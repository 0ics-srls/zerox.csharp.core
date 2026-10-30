§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DEVELOP⚡{/j-cvm-exec-plan,execution:run-20261030-zerox10rowversion}
@feature::10-db-row-version (the optimistic lock on the database's row version)
@branch::feature/10-db-row-version (from feature/09-single-load)
@plan::tasks/10-db-row-version/{notes,index,01-db,02-domain-api}.md{TDDAB#5,parsePlan✓,j-review-plan✓}
@block::05✓→next::FINAL-REVIEW (tools/verify.sh && tools/test-postgres.sh)
@consumer::CcV3 (the first consumer,submodule externals/zerox.csharp.core) found the defect;adapted by its own plan after this one

[WHY]
!found::©User 2026-10-30 "lo hai fatto male":Version incremented by SessionInterceptor (app-managed) → ExecuteUpdate|raw SQL|another process leave it unchanged → a later PUT with the old Version answered 200 and overwrote (lost update proven on PostgreSQL);PUT reloaded the row + CopyEditable + PinReadVersion = a patch on a design error
!decision::©User "è compito della EF gestirlo","nessun trigger","con sqlite l'utente sarà avvisato che non avrà optimistic lock","carico record con rowVersion=100,nel save EF vede che in db non c'è quel valore → rifiuta,nessun pin"
@design::[Timestamp] (IsRowVersion) on the type the provider generates (uint→xmin on PostgreSQL,ulong|long on SQL Server);save the client's row as sent (attach,editable fields modified);404|409 told apart only after a failed save;SQLite → default 0 + one warning;?version=N a ulong,TryDeleteById false when it does not fit;StaleVersionException → 409 for actions that read the row first;LastWriterWins only for the server's own change

[DECISIONS DURING EXECUTION]
>fixed::OpenAPI contract depended on the build machine's line endings (compiler writes XML docs with CRLF on Windows → summaries "\r\n")→ZeroxOpenApi.LineBreaks document transformer (OpenApiWalker+OpenApiVisitorBase,every text ReplaceLineEndings("\n"),last transformer)+OpenApiDocumentTests.Should_BreakEveryTextWithLf_WhateverThePlatformThatBuiltTheDocumentation{©User:"non deve dipendere da line endings,non azzardarti a girarlo"}
>fixed::Sample client lockfile named a registry (nexus.0ics.ai) → npm 12 EALLOWREMOTE elsewhere→client/.npmrc omit-lockfile-registry-resolved=true + lockfile without "resolved" (integrity kept){©User:"usi valori assoluti!!! non puoi!!!"}
>fixed::Vitest found no test under a folder with "#" (Vite cuts module ids at #)→client/vitest.config.mjs test.experimental.viteModuleRunner=false (Node imports the tests;tsconfig erasableSyntaxOnly)
>found::EF builds a model once per context type and shares it → a test watching the model building needs a context type of its own (RowVersionDbContext);TestDb.NewContext takes no logger
>removed::EntityFieldsTests.Should_NameTheFieldsCopyEditableWrites_WhenAsked with CopyEditable (one selection left,nothing to compare){plan:block 04 removes CopyEditable}
!env::zerox checkout inside CcV3 has no .claude/lint-rules links → ast-grep run with the pack's rules of CcV3 through a scratch sgconfig (same rules)

[NEXT]
?02-db-client-row-writes → 03 → 04 → 05 → FINAL REVIEW (tools/verify.sh && tools/test-postgres.sh)
?then CcV3 plan:TaskItem.Version→[Timestamp] uint (xmin)+migration dropping the column,trigger action compares the version first (StaleVersionException),FakeRepository new members,client regenerated
