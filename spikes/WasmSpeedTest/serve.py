# Static server for the spike's published builds, with the cross-origin isolation headers that WebAssembly threads need.
# python serve.py <port> <directory>
import http.server
import os
import sys
from functools import partial


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript", ".mjs": "text/javascript"}

    def send_head(self):
        # Blazor page routes (no file extension) fall back to the nearest index.html above them
        path = self.translate_path(self.path)
        if not os.path.exists(path) and "." not in os.path.basename(path):
            parent = os.path.dirname(path)
            while len(parent) >= len(self.directory) and not os.path.exists(os.path.join(parent, "index.html")):
                parent = os.path.dirname(parent)
            route = os.path.relpath(os.path.join(parent, "index.html"), self.directory).replace(os.sep, "/")
            self.path = "/" + route
        return super().send_head()

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


port, directory = int(sys.argv[1]), sys.argv[2]
http.server.ThreadingHTTPServer(("127.0.0.1", port), partial(Handler, directory=directory)).serve_forever()
