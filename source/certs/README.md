# Extra CA certificates for Docker builds

Only needed when your network intercepts TLS (e.g. a Fortinet FortiGate or other SSL-inspecting firewall). The web console
image's client stage copies this folder to `/certs` and points `NODE_EXTRA_CA_CERTS` at `/certs/corp-root-ca.crt`, so `npm ci`
trusts the firewall's CA. Without it, npm fails with `SELF_SIGNED_CERT_IN_CHAIN` and then `Exit handler never called!`.

To create `corp-root-ca.crt`, export the inspection CA from Windows as Base-64 PEM (certmgr.msc, or the PowerShell in the
commit that added this file), name it `corp-root-ca.crt` and place it here. Certificate files are not committed (see .gitignore).
