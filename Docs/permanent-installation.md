# Permanent Alexandria installation

The preview was migrated on 2026-10-12 and is now the permanent installation.

| Item | Location |
| --- | --- |
| Application | `https://192.168.0.106:10443` |
| Grafana | `https://192.168.0.106:10443/monitoring/` |
| Settings | `/home/spasmilenkov/alexandria/settings` |
| Source | `/home/spasmilenkov/alexandria/source` |
| Installer bundle | `/home/spasmilenkov/alexandria/installer` |
| Review or repair launcher | `/home/spasmilenkov/alexandria/run-installer` |
| Exact management commands | `/home/spasmilenkov/alexandria/settings/INSTALLATION.txt` |
| Public certificate trust instructions | `/home/spasmilenkov/alexandria/settings/nginx/CLIENT-TRUST.txt` |
| Protected migration evidence and rollback settings | `/home/spasmilenkov/alexandria/migration` |

Run the launcher from any directory. It explicitly selects the established settings and their
saved source folder. Starting the wizard does not deploy anything until the normal review and
installation steps. The full native bundle includes the required templates and the new opt-in
model downloader. Model sourcing for this installation remains offline by default; enable
HTTPS downloads explicitly in the Audio models step when needed. Existing verified models
are reused.

The migration retained project `alexandria-72d4c2f3b65f`, its generation, all 14 physical named
volumes and all 21 service image identities. Container ownership labels and application bind
mounts now use permanent paths. Source, settings, certificate identity, credentials, cached models
and existing file/account records were verified; services, HTTPS, repair ownership and monitoring
readiness passed. The maintainer reported the migrated installation works.

The original settings are a protected rollback copy under `migration/settings-before-migration`,
with a private pre-migration database dump. This is migration evidence, not an ongoing backup
schedule or a complete volume backup. Historical installer logs retain the paths used at the time.
The old temporary settings folder is no longer an active installation.

Data lives in the same Docker-managed named volumes. Regular backups must include the settings
folder and those volumes consistently. Credentials and current passwords were preserved; the
original bootstrap password does not replace a password changed through the application.

Grafana metrics remain available. Nontrivial Loki ingestion/Drilldown work remains deferred;
monitoring readiness alone does not establish log ingestion. No host tools or dependencies were
installed for the migration, and no images were pulled or rebuilt.

For moving another installation, use the [relocation procedure](../scripts/installer/README.md).
