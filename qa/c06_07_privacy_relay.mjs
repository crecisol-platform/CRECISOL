// QA-only loopback relay: observes actual Kestrel HTTP bytes before Angular's
// development proxy sends them to Chromium. Never changes product responses.
import { createServer, request } from 'node:http';

export async function startPrivacyRelay(apiPort, inspect, { deadlineMs=120000, maxBytes=4*1024*1024 }={}) {
  if(!Number.isInteger(apiPort)||apiPort<1||apiPort>65535||typeof inspect!=='function') throw new Error('Invalid isolated privacy relay configuration');
  const pending=new Set(), aborters=new Set(); let failure, closing=false;
  const record=error=>{failure??=error;};
  const server=createServer((incoming,outgoing)=>{
    outgoing.on('error',()=>{});
    const pathname=new URL(incoming.url,'http://127.0.0.1').pathname;
    const prefix=pathname.startsWith('/api/v1/platform-identity/')?'/api/v1/platform-identity/':pathname.startsWith('/api/v1/identity/')?'/api/v1/identity/':null;
    const op=prefix?(prefix.includes('platform-identity')?'platform/':'')+pathname.slice(prefix.length):null;
    // Only a fixed loopback upstream; never resolve a caller-selected host.
    const label=prefix?.includes('platform-identity')?'platform-identity':prefix?'tenant-identity':'unclassified';
    const task=new Promise((resolve,reject)=>{
      let done=false, upstream, timer;
      const fail=()=>{
        if(done)return;
        done=true;clearTimeout(timer);aborters.delete(fail);upstream?.destroy();outgoing.destroy();
        reject(new Error('C06-07 relay capture unavailable: '+label));
      };
      if(!incoming.url.startsWith('/api/')||incoming.url.startsWith('//')) {fail();return;}
      aborters.add(fail);timer=setTimeout(fail,deadlineMs);
      try {
        upstream=request({hostname:'127.0.0.1',port:apiPort,agent:false,path:incoming.url,method:incoming.method,headers:incoming.headers},response=>{
          const chunks=[];let size=0;
          response.on('error',fail);response.on('aborted',fail);
          response.on('data',chunk=>{
            if(done)return;
            size+=chunk.length;
            if(size>maxBytes){response.destroy();fail();return;}
            chunks.push(chunk);
          });
          response.on('end',()=>{
            if(done)return;
            if(!response.complete){fail();return;}
            const body=Buffer.concat(chunks,size);
            if(op){
              try {
                const raw=new TextDecoder('utf-8',{fatal:true}).decode(body);
                inspect(op,response.statusCode,raw,response.headers['cache-control'],incoming.headers,'relay');
              } catch(error) {
                // The oracle emits only fixed labels; decoding errors are neutral.
                record(error instanceof TypeError?new Error('C06-07 relay invalid UTF-8: '+label):error);
              }
            }
            done=true;clearTimeout(timer);aborters.delete(fail);
            // Even if the browser has navigated away, the real complete API
            // response was checked. Never substitute an artificial response.
            if(!outgoing.destroyed){
              try {outgoing.writeHead(response.statusCode,response.statusMessage,response.rawHeaders);outgoing.end(body);}
              catch {outgoing.destroy();record(new Error('C06-07 relay forwarding failed: '+label));}
            }
            resolve();
          });
        });
        upstream.on('error',fail);incoming.on('aborted',fail);incoming.on('error',fail);
        incoming.pipe(upstream);
      } catch {fail();}
    }).catch(record);
    pending.add(task);void task.then(()=>pending.delete(task));
  });
  server.requestTimeout=deadlineMs;server.headersTimeout=Math.min(deadlineMs,20000);
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(0,'127.0.0.1',()=>{server.off('error',reject);resolve();});});
  server.on('error',()=>record(new Error('C06-07 isolated relay server failure')));
  const drain=async()=>{while(pending.size)await Promise.all([...pending]);if(failure)throw failure;};
  const close=async()=>{
    if(closing)return;closing=true;
    for(const abort of [...aborters])abort();
    await new Promise(resolve=>{server.close(()=>resolve());server.closeAllConnections();});
  };
  return {port:server.address().port,drain,close,finish:async()=>{try{await drain();}finally{await close();}}};
}
