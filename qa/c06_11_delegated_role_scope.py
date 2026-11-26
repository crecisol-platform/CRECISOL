#!/usr/bin/env python3
"""C06-11 delegation policy and persistence slice: exact source scope, never a certificate."""
import os
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE="076118ab5370e1fcbc7bfe0f1caefe07a664365b"
PATHS=frozenset(["backend/src/Crecisol.Domain/Authorization/C06ScopedRoleDelegationPolicy.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Program.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Crecisol.C06Delegation.ContractsTests.csproj","docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md",".github/workflows/c06-11-delegated-role-policy.yml","qa/c06_11_delegated_role_scope.py","qa/c06_02_role_model_audit.py","database/migrations/024_c06_delegated_role_ledger.sql","database/migrations/024_c06_delegated_role_ledger.down.sql","database/tests/c06_11_delegated_role_mysql.sh","qa/c06_07_privacy_scope.py","qa/c06_08_query_scope.py","qa/c06_09_delivery_scope.py","qa/c06_10_offensive_scope.py","qa/c06_07_privacy_audits.py","qa/c06_09_delivery_audits.py","qa/c06_10_offensive_audits.py","backend/src/Crecisol.Infrastructure/Authorization/MySqlC06DelegationLedger.cs","backend/tests/Crecisol.C06Delegation.MySqlTests/Crecisol.C06Delegation.MySqlTests.csproj","backend/tests/Crecisol.C06Delegation.MySqlTests/Program.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Delegation.cs","backend/src/Crecisol.Application/Identity/C06DelegationPreparation.cs","database/migrations/025_c06_delegation_confirmation.sql","database/migrations/025_c06_delegation_confirmation.down.sql","backend/tests/Crecisol.C06Delegation.FlowTests/Program.cs","backend/tests/Crecisol.C06Delegation.FlowTests/Crecisol.C06Delegation.FlowTests.csproj","qa/c06_03_context_audits.py","qa/c06_04_abac_audits.py","qa/c06_05_stepup_audits.py","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegatedRead.cs","database/migrations/026_c06_delegation_authority.sql","database/migrations/026_c06_delegation_authority.down.sql","qa/c05_sha1_exclusive_audits.py","backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs","backend/src/Crecisol.Api/Contracts/C06DelegationHttpContract.cs","docs/c06/C06_11_CATALOGO_DELEGACION_V1.json","qa/c06_01_permissions_catalog_audit.py","backend/tests/Crecisol.C06Delegation.FlowTests/DelegationWireFixture.cs","qa/c06_06_exceptional_scope.py","backend/src/Crecisol.Application/Identity/C06DelegationInspection.cs","docs/c06/C06_11_CATALOGO_DELEGACION_V2.json","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegationCatalog.cs","backend/src/Crecisol.Application/Identity/C06DelegationCatalog.cs","docs/c06/C06_11_CATALOGO_DELEGACION_V3.json","frontend/src/app/identity/identity-api.service.ts","frontend/src/app/identity/delegation-client.model.ts","frontend/src/app/pages/delegation-editor.component.ts","frontend/src/app/pages/delegation-editor.component.html","frontend/src/app/pages/delegation-editor.component.css","frontend/src/app/pages/identity-flow-views.component.ts","frontend/src/app/pages/identity-workspace.component.html","frontend/tests/c06-delegation-editor.spec.ts"])
def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True).strip()
def need(ok,why):
    if not ok:
        raise AssertionError("C06-11 DELEGATION NO-GO: "+why)
def require_exact(paths):
    actual=frozenset(paths)
    need(len(paths)==len(actual) and actual==PATHS,
         "scope drift: "+repr(sorted(actual ^ PATHS)))
    return paths

# Precisely reviewed historical source edits only.
HISTORICAL_EDITS={
    "qa/c06_07_privacy_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_08_query_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_09_delivery_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_10_offensive_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_07_privacy_audits.py": [
        [
            "    end= \"3789224e49f3183563a7f8491a3b3101f161a6a1\" if any(p in changed for p in (\"docs/c06/C06_08_ESPECIFICACION.md\",\"docs/c06/C06_09_ESPECIFICACION.md\",\"docs/c06/C06_10_ESPECIFICACION.md\")) else \"HEAD\"",
            "    end= \"3789224e49f3183563a7f8491a3b3101f161a6a1\" if any(p in changed for p in (\"docs/c06/C06_08_ESPECIFICACION.md\",\"docs/c06/C06_09_ESPECIFICACION.md\",\"docs/c06/C06_10_ESPECIFICACION.md\",\"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\")) else \"HEAD\""
        ]
    ],
    "qa/c06_09_delivery_audits.py": [
        [
            "def inventory():\n",
            "def audited_product_end():\n    # Preserve the prior implementation's no-product-change invariant.\n    # Exact successor scope is independently checked; runtime controls use HEAD.\n    changed=git(\"diff\",\"--name-only\",BASE,\"HEAD\").splitlines()\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import historical_product_end\n        return historical_product_end()\n    return \"HEAD\"\n\ndef inventory():\n"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",\"frontend/\",\"database/migrations/\",\"database/operations/\",\"backend/src/Crecisol.CredentialSecurity/\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf\",\"docs/c06/C06_01_CATALOGO_ACCIONES.json\",\"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json\"),\"unapproved source or persistence change\")",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",\"frontend/\",\"database/migrations/\",\"database/operations/\",\"backend/src/Crecisol.CredentialSecurity/\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf\",\"docs/c06/C06_01_CATALOGO_ACCIONES.json\",\"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json\"),\"unapproved source or persistence change\")"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",\"database/migrations/\",\"database/operations/\"),\"persistence privilege drift\")",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",\"database/migrations/\",\"database/operations/\"),\"persistence privilege drift\")"
        ]
    ],
    "qa/c06_10_offensive_audits.py": [
        [
            "def scope():\n",
            "def audited_product_end():\n    # Preserve the prior implementation's no-product-change invariant.\n    # Exact successor scope is independently checked; runtime controls use HEAD.\n    changed=git(\"diff\",\"--name-only\",BASE,\"HEAD\").splitlines()\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import historical_product_end\n        return historical_product_end()\n    return \"HEAD\"\n\ndef scope():\n"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",path),\"unapproved product/authority change \"+path)",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",path),\"unapproved product/authority change \"+path)"
        ]
    ],
    "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs": [
        [
            "        if (r.Operation == \"sessions/keepalive\")",
            "        // Explicit C06-11 laboratory overlay; historical HTTP operations remain unchanged.\n        if (r.Operation == \"delegation/catalog\")\n            return await C06DelegationCatalogPage(u, tenant, actor, account, member, session, factor, idle, r, ct);\n        if (r.Operation is \"delegation/inspect\" or \"delegation/prepare\" or \"delegation/confirm\")\n            return await C06DelegationTransition(u, tenant, actor, account, member, session, factor, idle, r, ct);\n        if (r.Operation == \"delegation/read\")\n            return await C06ReadDelegatedCase(u, tenant, actor, account, member, session, factor, idle, r);\n        if (r.Operation == \"sessions/keepalive\")"
        ]
    ],
    "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs": [
        [
            "        public async Task<int> Run(string sql, params (string, object)[] parameters)",
            "        public Crecisol.Infrastructure.Authorization.MySqlC06DelegationLedger DelegationLedger() => new(db, tx);\n        public async Task<int> Run(string sql, params (string, object)[] parameters)"
        ]
    ],
    "qa/c06_03_context_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c06_04_abac_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c06_05_stepup_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c05_sha1_exclusive_audits.py": [
        [
            " ensure(bool(paths), \"empty current change scope\")",
            " ensure(bool(paths), \"empty current change scope\")\n if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in paths:\n  from c06_11_delegated_role_scope import require_exact\n  require_exact(paths)\n  return len(paths)"
        ]
    ],
    "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs": [
        [
            "                    (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));\n            }\n    }\n\n    private static async Task<IResult> Handle",
            "                    (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));\n            }\n        // C06-11 overlay: exact separate inventory, opt-in only.\n        if (C06DelegationHttpContract.Operations.Any(Operations.ContainsKey) ||\n            C06DelegationHttpContract.Operations.Any(C06BreakglassOperations.ContainsKey))\n            throw new InvalidOperationException(\"Delegation route inventory collision.\");\n        if (C06DelegationHttpContract.Enabled)\n            foreach (var operation in C06DelegationHttpContract.Operations)\n            {\n                var selected = operation;\n                endpoints.MapPost(\"/api/v1/identity/\" + selected,\n                    (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));\n            }\n    }\n\n    private static async Task<IResult> Handle"
        ],
        [
            "        var expectedFields = exceptional ? exceptionalFields! : Operations[operation];\n        // All identity responses, including denials and neutral receipts, are private.\n        context.Response.Headers.CacheControl = \"no-store, private\";\n        context.Response.Headers.Pragma = \"no-cache\";",
            "        var delegated = C06DelegationHttpContract.TryFields(operation, out var delegatedFields);\n        var expectedFields = delegated ? delegatedFields! : exceptional ? exceptionalFields! : Operations[operation];\n        // All identity responses, including denials and neutral receipts, are private.\n        context.Response.Headers.CacheControl = \"no-store, private\";\n        context.Response.Headers.Pragma = \"no-cache\";\n        if (delegated && !C06DelegationHttpContract.Enabled) return Results.NotFound();"
        ],
        [
            "                    if (name == \"code\" && !(operation == \"invitations/accept\" && value == \"enroll\")",
            "                    if (delegated && name == \"code\" && (value.Length != 6 || !value.All(char.IsAsciiDigit)))\n                        return Results.BadRequest();\n                    if (name == \"code\" && !(operation == \"invitations/accept\" && value == \"enroll\")"
        ],
        [
            "            if (!C06IdentityResponseProjection.TryApply(operation, result, out var safe))\n                return Results.Unauthorized();\n            if (browser) return browserTransport.Receipt(context, operation, safe, browserSession);\n            return safe.Allowed ? Results.Ok(safe.Value) : Results.Unauthorized();",
            "            if (!delegated)\n            {\n                if (!C06IdentityResponseProjection.TryApply(operation, result, out var safe))\n                    return Results.Unauthorized();\n                if (browser) return browserTransport.Receipt(context, operation, safe, browserSession);\n                return safe.Allowed ? Results.Ok(safe.Value) : Results.Unauthorized();\n            }\n            if (!C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe))\n                return Results.Unauthorized();\n            if (browser) return browserTransport.Receipt(context, operation, delegationSafe, browserSession);\n            return delegationSafe.Allowed ? Results.Ok(delegationSafe.Value) : Results.Unauthorized();"
        ]
    ],
    "qa/c06_01_permissions_catalog_audit.py": [
        [
            "def verify_catalog(data, source, program):\n",
            "def verify_delegation_overlay(source):\n    \"\"\"C06-11 is an explicit isolated successor, never a normative grant.\"\"\"\n    path=ROOT/\"docs/c06/C06_11_CATALOGO_DELEGACION_V3.json\"\n    marker=\"foreach (var operation in C06DelegationHttpContract.Operations)\"\n    contract_path=ROOT/\"backend/src/Crecisol.Api/Contracts/C06DelegationHttpContract.cs\"\n    if marker not in source:\n        need(not path.exists() and not contract_path.exists(),\"delegation overlay without route registration\")\n        return\n    need(source.count(marker)==1 and path.is_file() and contract_path.is_file(),\"delegation overlay missing\")\n    contract=contract_path.read_text(encoding=\"utf-8\")\n    pairs=re.findall(r'\\[\"([^\"]+)\"\\]\\s*=\\s*\\[([^\\]]*)\\]',contract)\n    actual={name:re.findall(r'\"([^\"]+)\"',fields) for name,fields in pairs}\n    expected={\"delegation/prepare\":[\"command\",\"branchId\",\"resourceId\",\"membershipId\",\"roleId\",\"roleRevision\",\"membershipRevision\",\"ceilingRevision\",\"grantRevision\",\"expiresAtUtc\",\"reason\"],\"delegation/confirm\":[\"command\",\"branchId\",\"resourceId\",\"membershipId\",\"roleId\",\"roleRevision\",\"membershipRevision\",\"ceilingRevision\",\"grantRevision\",\"expiresAtUtc\",\"reason\",\"challenge\",\"code\"],\"delegation/read\":[\"branchId\",\"resourceId\"],\"delegation/inspect\":[\"branchId\",\"resourceId\",\"membershipId\",\"roleId\"],\"delegation/catalog\":[\"cursor\"]}\n    responses={\"delegation/prepare\":[\"challenge\",\"expiresAtUtc\"],\"delegation/confirm\":[\"accepted\"],\"delegation/read\":[\"reference\"],\"delegation/inspect\":[\"roleCode\",\"roleRevision\",\"membershipRevision\",\"ceilingRevision\",\"grantRevision\",\"canAssign\",\"canRevoke\",\"maxExpiresAtUtc\"],\"delegation/catalog\":[\"items\",\"nextCursor\"]}\n    boundaries={\"delegation/prepare\":\"tenant_administrator_exact_scope\",\"delegation/confirm\":\"tenant_administrator_exact_scope_fresh_mfa\",\"delegation/read\":\"tenant_recipient_exact_resource_live_provenance\",\"delegation/inspect\":\"tenant_administrator_exact_candidate_advisory\",\"delegation/catalog\":\"tenant_administrator_bounded_live_candidate_catalog\"}\n    need(len(pairs)==5 and actual==expected,\"delegation request inventory drift\")\n    data=json.loads(path.read_text(encoding=\"utf-8\"))\n    previous=ROOT/\"docs/c06/C06_11_CATALOGO_DELEGACION_V2.json\"\n    need(previous.is_file(),\"historical delegation overlay missing\")\n    prior=json.loads(previous.read_text(encoding=\"utf-8\"))\n    need(prior[\"schema_version\"]==2 and prior[\"actions\"]==data[\"actions\"][:4],\n         \"historical delegation contracts changed\")\n    need(data[\"schema_version\"]==3 and data[\"previous_overlay\"]==\"docs/c06/C06_11_CATALOGO_DELEGACION_V2.json\" and\n         data[\"authority\"]==\"DOCUMENTATION_ONLY_NOT_RUNTIME_AUTHORIZATION\" and\n         data[\"base_operational_contracts\"]==79 and data[\"activated_normative\"]==[] and\n         data[\"feature_gate\"]==\"CRECISOL_C06_DELEGATION_LAB_ENABLED=true_and_C05_LOCAL_IDENTITY_ENABLED=true_and_local_isolation\",\n         \"delegation feature gate or premature normative activation\")\n    seen=set()\n    for row in data[\"actions\"]:\n        op=row[\"operation_code\"]\n        need(op in expected and op not in seen and row[\"required_fields\"]==expected[op] and\n             row[\"response_fields\"]==responses[op] and row[\"boundary\"]==boundaries[op] and\n             row[\"action_id\"]==\"identity.\"+op.replace(\"/\",\".\") and row[\"http_method\"]==\"POST\" and\n             row[\"http_route\"]==\"/api/v1/identity/\"+op and row[\"source_file\"]==SOURCE and row[\"realm\"]==\"tenant\" and\n             row[\"authorization_from\"]==\"backend_only\" and row[\"general_role_grantable\"] is False and\n             row[\"ui_grant_source\"] is False and row[\"registered_in_local_api\"] is True and\n             row[\"denied_payload\"]==\"empty\" and row[\"cache_control\"]==\"no-store, private\",\n             \"delegation route/response boundary drift\")\n        seen.add(op)\n    need(seen==set(expected),\"delegation routes missing\")\n    need(data[\"negative_scenarios\"]==[\"anonymous\",\"forged_bearer\",\"wrong_host\",\"injected_tenant\",\"extra_field\",\"duplicate_field\",\"wrong_origin\",\"browser_without_csrf\"],\"delegation negative matrix drift\")\n    for token in ('if (C06DelegationHttpContract.Enabled)',marker,'endpoints.MapPost(\"/api/v1/identity/\" + selected,',\n                  'if (delegated && !C06DelegationHttpContract.Enabled) return Results.NotFound();',\n                  'C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe)',\n                  'browserTransport.Receipt(context, operation, delegationSafe, browserSession)'):\n        need(token in source,\"delegation transport gate missing \"+token)\n    need(source.index(\"C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe)\")<\n         source.index(\"Results.Ok(delegationSafe.Value)\"),\"delegation raw response bypass\")\n    need('Environment.GetEnvironmentVariable(\"CRECISOL_C06_DELEGATION_LAB_ENABLED\") == \"true\"' in contract and\n         \"if(!result.Allowed) return true\" in contract and \"C06IdentityResponseProjection.TryApply(\" in contract and\n         \"JsonSerializer.Serialize\" not in contract and \"inspection.CanAssign==inspection.CanRevoke\" in contract and\n         \"inspection.GrantRevision.ToString(CultureInfo.InvariantCulture)\" in contract and\n         \"catalog.Items.Count>8\" in contract and 'TryApply(\"delegation/inspect\",result with {Value=item.Inspection}' in contract,\"delegation projection/privacy drift\")\n\ndef verify_catalog(data, source, program):\n"
        ],
        [
            "    verify_exception_overlay(source)\n    rows = data.get(\"api_actions\")",
            "    verify_exception_overlay(source)\n    verify_delegation_overlay(source)\n    rows = data.get(\"api_actions\")"
        ]
    ],
    "qa/c06_06_exceptional_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        return require_exact(changed)\n"
        ]
    ],
    "frontend/src/app/identity/identity-api.service.ts": [
        [
            "import { canonicalPhotoPixels } from './canonical-photo-pixels';",
            "import { canonicalPhotoPixels } from './canonical-photo-pixels';\nimport { decodeDelegationPage, decodeInspection, decodeDelegationChallenge, decodeDelegationAccepted,\n  exactSelection, type DelegationSelection, type DelegationIntent, type DelegationPage,\n  type DelegationInspection, type DelegationChallenge } from './delegation-client.model';"
        ],
        [
            "  private sessionRevision = 0;",
            "  private sessionRevision = 0;\n  private readonly delegationContextValue = signal(0);\n  readonly delegationContext = this.delegationContextValue.asReadonly();"
        ],
        [
            "        this.stateValue.set('authenticated');",
            "        this.delegationContextValue.update(v => v + 1);\n        this.stateValue.set('authenticated');"
        ],
        [
            "    this.enrollmentRequired.set(false);\n    this.stateValue.set('authenticated');",
            "    this.enrollmentRequired.set(false);\n    this.delegationContextValue.update(v => v + 1);\n    this.stateValue.set('authenticated');"
        ],
        [
            "  async readPrivate(branchId: string, resourceId: string): Promise<void> {",
            "  // Only the four closed operations are callable by this editor. Check\n  // both C05 session revision and context epoch before accepting a response.\n  private async delegationPost(operation: 'catalog'|'inspect'|'prepare'|'confirm', body: object): Promise<unknown> {\n    const revision=this.sessionRevision, context=this.delegationContext();\n    if(this.state()!=='authenticated') throw new Error('identity_unavailable');\n    const response=await this.post<unknown>('delegation/'+operation,body);\n    if(revision!==this.sessionRevision || context!==this.delegationContext() ||\n        this.state()!=='authenticated') throw new Error('identity_unavailable');\n    return response;\n  }\n  async delegationCatalog(cursor: string): Promise<DelegationPage> {\n    return decodeDelegationPage(await this.delegationPost('catalog',{cursor}));\n  }\n  async delegationInspect(selection: DelegationSelection): Promise<DelegationInspection> {\n    return decodeInspection(await this.delegationPost('inspect',exactSelection(selection)));\n  }\n  async delegationPrepare(intent: DelegationIntent): Promise<DelegationChallenge> {\n    return decodeDelegationChallenge(await this.delegationPost('prepare',intent));\n  }\n  async delegationConfirm(intent: DelegationIntent, challenge: string, code: string): Promise<void> {\n    decodeDelegationAccepted(await this.delegationPost('confirm',{...intent,challenge,code}));\n  }\n\n  async readPrivate(branchId: string, resourceId: string): Promise<void> {"
        ],
        [
            "    ++this.sessionRevision;\n    ++this.pendingRequestRevision;",
            "    ++this.sessionRevision;\n    this.delegationContextValue.update(v => v + 1);\n    ++this.pendingRequestRevision;"
        ]
    ],
    "frontend/src/app/pages/identity-flow-views.component.ts": [
        [
            "import { IdentityApiService } from '../identity/identity-api.service';",
            "import { IdentityApiService } from '../identity/identity-api.service';\nimport { DelegationEditorComponent } from './delegation-editor.component';"
        ],
        [
            "  imports: [InstitutionalModalComponent, InstitutionalCardComponent, InstitutionalStateComponent, SecureFieldFeedbackDirective],",
            "  imports: [InstitutionalModalComponent, InstitutionalCardComponent, InstitutionalStateComponent, SecureFieldFeedbackDirective, DelegationEditorComponent],"
        ]
    ],
    "frontend/src/app/pages/identity-workspace.component.html": [
        [
            "  <!-- Permission details remain keyboard-accessible after the task-first queue. -->",
            "  <cs-delegation-editor />\n  <!-- Permission details remain keyboard-accessible after the task-first queue. -->"
        ]
    ]
}
HISTORICAL_MODS=frozenset(HISTORICAL_EDITS) | {"qa/c06_02_role_model_audit.py"}

def historical_product_end():
    need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,
         "missing ratified historical baseline")
    paths=git("diff","--name-only",BASE,"HEAD").splitlines()
    require_exact(paths)
    status=git("diff","--name-status","--no-renames",BASE,"HEAD").splitlines()
    expected={"M\t"+p if p in HISTORICAL_MODS else "A\t"+p for p in PATHS}
    need(len(status)==len(expected) and set(status)==expected,"unapproved path status")
    for path,pairs in HISTORICAL_EDITS.items():
        expected_text=git("show",BASE+":"+path)
        for old,new in pairs:
            need(expected_text.count(old)==1,"historical marker drift "+path)
            expected_text=expected_text.replace(old,new)
        need((ROOT/path).read_text(encoding="utf-8").strip()==expected_text,
             "historical audit modified beyond exact successor change: "+path)
    return BASE

def main():
    historical_product_end()
    need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,"missing verified base")
    need(os.environ.get("GITHUB_EVENT_NAME")=="pull_request" and os.environ.get("GITHUB_BASE_REF")=="dev","only draft candidate PR to dev")
    paths=git("diff","--name-only",BASE,"HEAD").splitlines()
    require_exact(paths)
    require_exact(git("diff","--name-only","origin/dev...HEAD").splitlines())
    need(set(git("log","--format=","--name-only","--no-renames",BASE+"..HEAD").splitlines())-{""} <= PATHS,"historic scope drift")
    for path in ("backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs",
                 "backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs",
                 "database/migrations/019_c06_prepared_roles.sql",
                 "backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs",
                 "docs/c06/C06_01_CATALOGO_ACCIONES.json",
                 "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json",
                 "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"):
        need(git("rev-parse",BASE+":"+path)==git("rev-parse","HEAD:"+path),"historical source changed: "+path)
    # The successor router itself must be the only permitted change to the
    # legacy auditor. Compare against the ratified version, not markers.
    old=git("show",BASE+":qa/c06_02_role_model_audit.py")
    current=(ROOT/"qa/c06_02_role_model_audit.py").read_text(encoding="utf-8").strip()
    marker='    # Explicit sequential successor: preserve every other historical audit.'
    insertion='    if "docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed\n'
    need(old.count(marker)==1 and current==old.replace(marker,marker+"\n"+insertion.rstrip("\n")),
         "historical auditor modified beyond exact successor routing")
    for attack in (paths[:-1],paths+[paths[0]],paths+["frontend/src/unreviewed.ts"],
                   paths+["backend/src/Crecisol.Api/Unreviewed.cs"],
                   paths+["database/migrations/unreviewed.sql"],
                   paths+["docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"]):
        try:
            require_exact(attack)
        except AssertionError:
            continue
        raise AssertionError("scope mutation passed")
    up=(ROOT/"database/migrations/024_c06_delegated_role_ledger.sql").read_text(encoding="utf-8")
    down=(ROOT/"database/migrations/024_c06_delegated_role_ledger.down.sql").read_text(encoding="utf-8")
    import re
    sql=re.sub(r"(?m)^\s*--[^\n]*$", "", up)
    # Quoted error messages may contain the word grant; inspect executable SQL.
    def no_authority_change(source):
        unquoted=re.sub(r"'(?:''|[^'])*'", "''", source)
        return re.search(r"\b(?:GRANT|ALTER\s+TABLE|DROP\s+TABLE)\b",unquoted,re.I) is None
    need(no_authority_change(sql),"new ledger changes old privileges/schema")
    confirmation=(ROOT/"database/migrations/025_c06_delegation_confirmation.sql").read_text(encoding="utf-8")
    executable=re.sub(r"(?m)^\s*--[^\n]*$", "", confirmation)
    need(no_authority_change(executable),"confirmation migration expands legacy privileges/schema")
    need(git("rev-parse","b66f5bd4e746c23b79ed468f06ee5a9ce26cfb53:database/migrations/024_c06_delegated_role_ledger.sql")==git("rev-parse","HEAD:database/migrations/024_c06_delegated_role_ledger.sql"),"approved ledger changed")
    for invariant in ("uq_c06_dcf_token","fk_c06_dcf_member","fk_c06_dcf_session","trg_c06_dcf_consume","trg_c06_dcf_no_delete","TIMESTAMPDIFF(MICROSECOND"):
        need(invariant in executable,"missing confirmation invariant "+invariant)
    transport=read_text=(ROOT/"backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs").read_text(encoding="utf-8")
    from c06_01_permissions_catalog_audit import verify_delegation_overlay
    verify_delegation_overlay(transport)
    editor=(ROOT/"frontend/src/app/pages/delegation-editor.component.ts").read_text(encoding="utf-8")
    template=(ROOT/"frontend/src/app/pages/delegation-editor.component.html").read_text(encoding="utf-8")
    client=(ROOT/"frontend/src/app/identity/identity-api.service.ts").read_text(encoding="utf-8")
    for invariant in ("delegationCatalog('start')","delegationInspect(row)","delegationPrepare(intent)",
                      "delegationConfirm(intent,proof.challenge,code)","this.intent=null","this.challenge.set(null)",
                      "this.identity.delegationContext()"):
        need(invariant in editor,"missing editor protection "+invariant)
    for forbidden in ("localStorage","sessionStorage","indexedDB","innerHTML"):
        need(forbidden not in editor and forbidden not in template,"editor persists or injects private values")
    need('@if (available())' in template and 'autocomplete="one-time-code"' in template and
         "context!==this.delegationContext()" in client,"editor capability or session gate missing")
    need(git("rev-parse","ade79ca72b353824fe46ee98782b2390111446ac:docs/c06/C06_11_CATALOGO_DELEGACION_V2.json")==git("rev-parse","HEAD:docs/c06/C06_11_CATALOGO_DELEGACION_V2.json"),"v2 HTTP overlay changed")
    catalog=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegationCatalog.cs").read_text(encoding="utf-8")
    for invariant in ("c.tenant_id=@t AND c.actor_membership_id=@m","LIMIT 9 FOR SHARE","rows.Take(DelegationPageSize)",
                      'Operation="delegation/inspect"',"Protect(plain,purpose)","Unprotect(envelope,purpose)",
                      'Number(session,"session_revision")',"entitlementRevision.ToString","u.Subject=originalSubject"):
        need(invariant in catalog,"catalog missing "+invariant)
    for forbidden in ("u.Run(","AcceptTotp(","ledger.AppendAsync(","SELECT display_reference","protected_profile"):
        need(forbidden not in catalog,"catalog writes authority or loads private content")
    need(git("rev-parse","472806a84508ae947f45b097280abbaafabb46c1:docs/c06/C06_11_CATALOGO_DELEGACION_V1.json")==git("rev-parse","HEAD:docs/c06/C06_11_CATALOGO_DELEGACION_V1.json"),"v1 HTTP overlay changed")
    inspection_source=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Delegation.cs").read_text(encoding="utf-8")
    inspection=inspection_source.split("        if(inspecting)\n",1)[1].split("        if(!C06ScopedRoleDelegationPolicy.CanPrepare(e))",1)[0]
    for invariant in ("ExpectedRoleRevision=e.RoleRevision","ExpectedGrantRevision=existing?.Revision??0",
                      "C06ScopedRoleDelegationPolicy.CanPrepare(e with","if(!canAssign && !canRevoke) return new(false)",
                      "new C06DelegationInspection"):
        need(invariant in inspection,"inspection bypass "+invariant)
    for forbidden in ("u.Run(","AcceptTotp(","ledger.AppendAsync(","Token()","display_reference"):
        need(forbidden not in inspection,"inspection writes authority or loads private content")
    need("C06DelegationHttpContract.Enabled" in transport and
         "C06DelegationHttpContract.TryApply(operation, result, out var delegationSafe)" in transport,
         "unreviewed delegation HTTP exposure")
    wire=(ROOT/"backend/tests/Crecisol.C06Delegation.FlowTests/DelegationWireFixture.cs").read_text(encoding="utf-8")
    tests=(ROOT/"backend/tests/Crecisol.C06Delegation.FlowTests/Program.cs").read_text(encoding="utf-8")
    for invariant in ('app.MapLocalOperationalIdentity()', '127.0.0.1:0','UseCookies=false','UseProxy=false'):
        need(invariant in wire,"wire fixture missing "+invariant)
    for invariant in ('HTTP negative matrix leaves authority unchanged','HTTP recipient exact reference',
                      'HTTP projection denial strips arbitrary payload','HTTP projection rejects extra private fields',
                      'await WireDenied("delegation/read",readFields,HttpStatusCode.Unauthorized,subjectToken,browser:true)'):
        need(invariant in tests,"wire oracle missing "+invariant)
    candidate=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Delegation.cs").read_text(encoding="utf-8")
    for invariant in ("CRECISOL_C06_DELEGATION_LAB_ENABLED", "HasCurrentTerms(u,tenant,actor)", "C06ScopedRoleDelegationPolicy.CanPrepare(e)", "Number(count,\"pending\")>=3", "CryptographicOperations.FixedTimeEquals(", "await AcceptTotp(u,actor,stepFactor,s.Code!,activate:false)", "await ledger.AppendAsync(proved"):
        need(invariant in candidate,"delegation confirmation missing "+invariant)
    need(candidate.index("CryptographicOperations.FixedTimeEquals(")<candidate.index("await AcceptTotp(")<candidate.index("UPDATE c06_delegation_confirmation SET consumed_at_utc=@now")<candidate.index("await ledger.AppendAsync(proved"),"confirmation proof/write sequence drift")
    provenance=(ROOT/"database/migrations/026_c06_delegation_authority.sql").read_text(encoding="utf-8")
    need(no_authority_change(re.sub(r"(?m)^\s*--[^\n]*$", "", provenance)),"provenance migration expands historical authority")
    need(git("rev-parse","4267ea67dbf69f5c37fae1a2fd368cc8fe21d44e:database/migrations/025_c06_delegation_confirmation.sql")==git("rev-parse","HEAD:database/migrations/025_c06_delegation_confirmation.sql"),"approved confirmation migration changed")
    reader=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegatedRead.cs").read_text(encoding="utf-8")
    for invariant in ("JOIN c06_delegation_authority", "v.grant_id IS NULL","src.grant_revision=a.source_revision",
                      "ent.revision=a.entitlement_revision","x.owner_account_id=a.owner_account_id",
                      "sa.security_revision=a.subject_account_revision","ia.security_revision=a.issuer_account_revision",
                      "c.revision=g.ceiling_revision","role.revision=g.role_revision","sources.Count!=1"):
        need(invariant in reader,"missing delegated read invariant "+invariant)
    need(reader.index("C06CaseAbacPolicy.Allows(proof)")<reader.index("SELECT display_reference"),"private content selected before delegated ABAC")
    need(candidate.index("await ledger.AppendAsync(proved")<candidate.index("INSERT INTO c06_delegation_authority"),"provenance written outside confirmed issuance")
    for forbidden in ("GRANT SELECT ON sample TO sample_role",
                      "ALTER TABLE sample ADD COLUMN unsafe INT",
                      "DROP TABLE sample"):
        need(not no_authority_change(sql+"\n"+forbidden+";"),"dangerous DDL mutation accepted")
    need(no_authority_change("SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant immutable';"),
         "quoted error message incorrectly treated as a privilege grant")
    for invariant in ("uq_c06_dg_scope","fk_c06_dc_actor","fk_c06_dc_resource","fk_c06_dg_issuer_scope",
                      "fk_c06_dv_exact","trg_c06_dg_no_update","trg_c06_dv_no_delete","ck_c06_dg_no_self",
                      "NEW.expires_at_utc<=c.valid_until_utc","NEW.expires_at_utc<=s.expires_at_utc"):
        need(invariant in sql,"missing ledger invariant "+invariant)
    need("IF EXISTS(SELECT 1 FROM c06_delegation_revocation)" in down and
         "ledger not empty; preserve history" in down,"unsafe down migration")
    for path in ("database/operations/c05_local_runtime_role.sql","database/operations/c06_04_local_runtime_role.sql"):
        need(git("rev-parse",BASE+":"+path)==git("rev-parse","HEAD:"+path),"runtime grant source changed")
    print("PASS C06-11 DELEGATION fifty-four isolated paths, six rejected mutations; exact C06-02/07/08/09/10 successor routing, prior contracts and Master unchanged")

if __name__=="__main__":
    main()
