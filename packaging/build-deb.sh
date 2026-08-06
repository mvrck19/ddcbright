#!/bin/sh
# Builds ddcbright.deb by staging the live ddcbright/ package alongside the
# checked-in packaging metadata in packaging/deb/. Nothing here is committed
# as a duplicate copy of the source, so it can't go stale the way the old
# ddcbright-1.0/ddcbright-1.1 trees did.
set -e

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT

cp -r "$repo_root/packaging/deb/." "$staging/"
mkdir -p "$staging/usr/share/ddcbright/ddcbright"
cp "$repo_root"/ddcbright/*.py "$repo_root/ddcbright/sun.png" "$staging/usr/share/ddcbright/ddcbright/"
chmod 755 "$staging/usr/bin/ddcbright"

dpkg-deb --build --root-owner-group "$staging" "$repo_root/ddcbright.deb"
echo "Built $repo_root/ddcbright.deb"
