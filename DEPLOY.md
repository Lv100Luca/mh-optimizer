# Deploying the browser app

The site is <https://mh-optimizer.luca-diegel.de>: the published `wwwroot` of `src/MHWildsOptimizer.Browser`, served
straight off the VPS disk by Caddy. There is no server code, container or database; everything runs in the browser.

[`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) runs the tests and publishes the app on every PR and push.
A push to `main` (or **Actions → Build and deploy → Run workflow** on `main`) also copies the site to
`/opt/mh-optimizer/site` with rsync and checks that the page comes back with the cross-origin isolation headers. A
deploy restarts nothing and swaps the new files in at the end, so it can run at any time.

| What                 | Where                                                                      |
|----------------------|----------------------------------------------------------------------------|
| VPS                  | `159.195.194.247`, SSH on port 628                                         |
| Site files           | `/opt/mh-optimizer/site`, owned by `mh-deploy`                             |
| Caddy block          | [`deploy/Caddyfile`](deploy/Caddyfile), appended to `/etc/caddy/Caddyfile` |
| Repo variables       | `VPS_HOST`, `VPS_USER` (`mh-deploy`), `VPS_SSH_PORT` (`628`)               |
| Repo secret          | `VPS_SSH_KEY`, the private half of the deploy key                          |

## One-time setup

**1. DNS.** An A record `mh-optimizer.luca-diegel.de → 159.195.194.247` (DNS only, no proxy).

**2. The deploy user and the site directory**, on the VPS:

```bash
sudo apt install rsync                       # also brings rrsync, which the key below is locked to
sudo adduser --disabled-password --gecos "" mh-deploy
sudo mkdir -p /opt/mh-optimizer/site
sudo chown mh-deploy:mh-deploy /opt/mh-optimizer/site
sudo chmod 755 /opt/mh-optimizer /opt/mh-optimizer/site    # Caddy runs as its own user and must traverse both
```

If `/etc/ssh/sshd_config` has an `AllowUsers` line, add `mh-deploy` to it and `sudo systemctl reload ssh`.

**3. The deploy key.** Make a key pair on your machine, used for nothing else:

```bash
ssh-keygen -t ed25519 -N "" -C "mh-optimizer deploy" -f mh-deploy
```

Put the public half (`mh-deploy.pub`) on the VPS, locked to writing into the site directory and nothing else:

```bash
sudo -u mh-deploy mkdir -p -m 700 /home/mh-deploy/.ssh
echo 'command="rrsync -wo /opt/mh-optimizer/site",restrict <contents of mh-deploy.pub>' | sudo -u mh-deploy tee /home/mh-deploy/.ssh/authorized_keys
sudo chmod 600 /home/mh-deploy/.ssh/authorized_keys
```

`rrsync` must be on the PATH (`command -v rrsync`; it ships with rsync 3.2.4 and later). Because of it, the workflow's
rsync target is the bare `mh-deploy@host:`, which rrsync maps to `/opt/mh-optimizer/site`, and the key cannot open a
shell.

Then store the private half in the repo and delete both files:

```bash
gh secret set VPS_SSH_KEY -R Lv100Luca/mh-optimizer < mh-deploy
```

**4. Caddy.** Append [`deploy/Caddyfile`](deploy/Caddyfile) to `/etc/caddy/Caddyfile`, then:

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy gets the certificate on the first request. Until the first deploy the directory is empty and the site answers 404.

**5. Deploy.** Push to `main`, or run the workflow from the Actions tab.

## Verify

```bash
curl -sI https://mh-optimizer.luca-diegel.de/ | grep -i cross-origin    # both headers: the solver needs them
curl -sI https://mh-optimizer.luca-diegel.de/solver/bridge.js | head -1  # 200
```

In the browser, `crossOriginIsolated` in the console must be `true`; Options shows how many solver threads it got.

## Things that bite

- **The headers are load-bearing.** Without `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` there is no
  `SharedArrayBuffer`, so no WebAssembly threads. `coi-sw.js` papers over it with a service worker and a reload, but the
  smoke test fails the deploy on purpose so it does not go unnoticed.
- **Everything is `Cache-Control: no-cache`**, which still caches but revalidates (a 304 when unchanged). `index.html`,
  `data/*.json` and the solver bundle keep their names across releases, so a longer cache would serve a stale mix.
- **Profiles live in each visitor's browser storage**, keyed by the site's origin. Moving the site to another domain
  starts everyone empty; export the profile (profile menu) first and import it on the new domain.
