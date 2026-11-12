#!/bin/sh
# Setzt den Basispfad einer veröffentlichten Demo, z. B. für die Auslieferung unter /demos/zeiterfassung/.
# Aufruf: docker/set-base-path.sh <Ordner mit index.html> <Basispfad>
#   docker/set-base-path.sh publish/wwwroot /demos/zeiterfassung/
set -eu
dir="$1"; base="$2"
case "$base" in /*/) ;; *) echo "Basispfad muss mit / beginnen und enden, z. B. /demos/zeiterfassung/" >&2; exit 1;; esac
sed -i "s#<base href=\"/\" />#<base href=\"$base\" />#" "$dir/index.html"
grep -q "<base href=\"$base\" />" "$dir/index.html" || { echo "Basispfad nicht gesetzt" >&2; exit 1; }
# Vorkomprimierte Kopien von index.html sind jetzt veraltet.
rm -f "$dir/index.html.br" "$dir/index.html.gz"
echo "Basispfad in $dir/index.html: $base"
