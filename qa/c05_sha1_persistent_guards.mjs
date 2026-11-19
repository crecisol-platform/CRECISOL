// Pure fail-closed checks for the C05-only local persistent QA supervisor.
// No Docker, MySQL, network, or filesystem operations are permitted here.
export function assertSafeMigrationLedger(priorTableCount, applied, expectedVersions) {
  if (!Number.isSafeInteger(priorTableCount) || priorTableCount < 0 ||
      !Number.isSafeInteger(expectedVersions) || expectedVersions < 1 ||
      !(applied instanceof Map)) {
    throw new Error('Invalid C05 migration metadata; refusing startup.');
  }
  if (priorTableCount === 0 && applied.size === 0) return 'fresh';
  if (priorTableCount > 0 && applied.size === expectedVersions &&
      Array.from({ length: expectedVersions }, (_, i) => i + 1).every(version =>
        /^[0-9a-f]{64}$/i.test(applied.get(version) || ''))) {
    return 'complete';
  }
  throw new Error('Existing or partially migrated C05 database requires verified backup/restore, API shutdown and approved offline SHA1 operator rekey; refusing implicit migration.');
}

export function assertC05ContainerIdentity(mounts, ports, configuredImage,
    expectedVolume, expectedPasswordFile, expectedImage) {
  if (!Array.isArray(mounts) || !ports || typeof ports !== 'object' ||
      configuredImage !== expectedImage ||
      mounts.filter(item => item.Type === 'volume' &&
        item.Name === expectedVolume && item.Destination === '/var/lib/mysql').length !== 1 ||
      mounts.filter(item => item.Type === 'bind' &&
        item.Source === expectedPasswordFile &&
        item.Destination === '/run/secrets/mysql-root-password' && item.RW === false).length !== 1 ||
      !Array.isArray(ports['3306/tcp']) || ports['3306/tcp'].length !== 1 ||
      ports['3306/tcp'][0].HostIp !== '127.0.0.1' ||
      ports['3306/tcp'][0].HostPort !== '3310') {
    throw new Error('Existing Docker container is not the isolated pinned C05 MySQL instance; refusing use.');
  }
}
