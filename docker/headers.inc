# Die Demo braucht keine Verbindung nach außen: connect-src 'self' sperrt sie.
# 'wasm-unsafe-eval' ist für WebAssembly nötig. 'unsafe-inline' bei script-src braucht es für die Importmap
# in index.html (ihr Inhalt ändert sich mit jedem Build, ein fester Hash ginge nicht).
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'" always;
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "no-referrer" always;
