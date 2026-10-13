# Relocating an established installation

`relocate-settings.py` prepares a new settings folder without controlling Docker or deleting
the original. It uses Python's standard library and requires no package installation. It accepts
only complete, matching current metadata/checkpoints, verifies every recorded artifact, rejects
symlinks and existing destinations, and checks the permanent source is an unchanged copy.
Credentials, certificates, generation/project identity, volume mapping and cached models survive.

Copy the original source to its permanent folder first, then prepare the settings:

```bash
python3 scripts/installer/relocate-settings.py \
  --settings '/old/installation settings' \
  --destination '/permanent/settings' \
  --source '/permanent/source'
```

Keep the original settings/source as rollback copies until verification. The utility updates
metadata, Compose paths, current installation/trust instructions and checkpoint hashes. Historical
logs retain their original paths. It points model sourcing to the copied installation cache.

Before restarting, privately compare resolved old/new Compose: only the approved path changes
should differ. Record all container image IDs and physical volume names, and make a consistent
settings/database/data backup. Confirm every existing image reference still resolves to its
recorded image ID. Never print resolved Compose or environment values into shared logs.

Use the **same saved project name** and explicit new settings arguments for migration:

```bash
docker compose --project-name '<saved project>' \
  --project-directory '/permanent/settings' \
  --env-file '/permanent/settings/.env' \
  --file '/permanent/settings/docker-compose.yml' stop

docker compose --project-name '<saved project>' \
  --project-directory '/permanent/settings' \
  --env-file '/permanent/settings/.env' \
  --file '/permanent/settings/docker-compose.yml' \
  up -d --no-build --pull never --force-recreate
```

Recreating every container, including exited setup jobs, updates Docker's working-directory and
configuration-file ownership labels as well as bind mounts. A plain folder copy followed by
Builder repair is insufficient: repair deliberately rejects the old ownership labels.

Verify image IDs, physical volumes and existing data are unchanged; every application bind
mount and Compose label uses the permanent paths; services, HTTPS and established login work;
and Builder recognizes a complete checkpoint and accepts ownership at the new location.
Do not use volume deletion or pruning commands during this procedure.

Keep the full native installer bundle, including `Templates/Local`, at its permanent location.
To review or repair the relocated installation, run:

```bash
'/permanent/installer/Builder' --settings '/permanent/settings'
```

The wizard preselects the saved source/settings and still requires its normal review before
deployment. Starting it does not itself change or redeploy the stack.

Offline utility tests:

```bash
python3 -m unittest discover -s scripts/installer/tests -v
```
