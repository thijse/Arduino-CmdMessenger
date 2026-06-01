"""WebForm — base class hiding FastAPI + WebSocket complexity.

Equivalent of ``ChartForm.cs`` / ``ControllerForm.cs`` in the C# samples.
The user never needs to look inside; they just call high-level methods like
:meth:`update_chart` and :meth:`send_to_clients`.

Requirements (install via ``pip install py-cmdmessenger[web]``):
- fastapi >= 0.100
- uvicorn[standard] >= 0.20
"""
from __future__ import annotations

import asyncio
import json
import os
import threading
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional

try:
    from fastapi import FastAPI, WebSocket, WebSocketDisconnect
    from fastapi.staticfiles import StaticFiles
    import uvicorn
except ImportError as exc:  # pragma: no cover
    raise ImportError(
        "Web samples require 'fastapi' and 'uvicorn'. "
        "Install with: pip install py-cmdmessenger[web]"
    ) from exc


class _Broadcaster:
    """Manages connected WebSocket clients and broadcasts messages."""

    def __init__(self) -> None:
        self._clients: List[WebSocket] = []
        self._lock = threading.Lock()

    async def connect(self, ws: WebSocket) -> None:
        await ws.accept()
        with self._lock:
            self._clients.append(ws)

    def disconnect(self, ws: WebSocket) -> None:
        with self._lock:
            try:
                self._clients.remove(ws)
            except ValueError:
                pass

    async def broadcast(self, data: dict) -> None:
        payload = json.dumps(data)
        with self._lock:
            clients = list(self._clients)
        for client in clients:
            try:
                await client.send_text(payload)
            except Exception:
                self.disconnect(client)

    def broadcast_sync(self, data: dict) -> None:
        """Thread-safe broadcast from non-async code (e.g. CmdMessenger callback)."""
        if self._loop is None:
            return
        asyncio.run_coroutine_threadsafe(self.broadcast(data), self._loop)

    _loop: Optional[asyncio.AbstractEventLoop] = None


class WebForm:
    """Base class for web-based sample UIs.

    Starts a FastAPI server in a background thread. Subclasses / users call
    :meth:`update_chart` or :meth:`send_to_clients` from CmdMessenger callbacks
    (which run on arbitrary threads) — the WebSocket broadcast is scheduled onto
    the asyncio event loop via :func:`asyncio.run_coroutine_threadsafe`.
    """

    def __init__(
        self,
        title: str = "CmdMessenger",
        port: int = 8080,
        static_dir: Optional[str] = None,
    ) -> None:
        self.title = title
        self.port = port
        self._static_dir = static_dir
        self._broadcaster = _Broadcaster()
        self._app = FastAPI(title=title)
        self._server: Optional[uvicorn.Server] = None
        self._thread: Optional[threading.Thread] = None
        self._command_handlers: Dict[str, Callable] = {}

        # --- WebSocket endpoint ---
        @self._app.websocket("/ws")
        async def websocket_endpoint(ws: WebSocket):
            await self._broadcaster.connect(ws)
            try:
                while True:
                    text = await ws.receive_text()
                    # Handle commands from the browser.
                    try:
                        msg = json.loads(text)
                        cmd_name = msg.get("command")
                        if cmd_name and cmd_name in self._command_handlers:
                            self._command_handlers[cmd_name](msg.get("value"))
                    except (json.JSONDecodeError, KeyError):
                        pass
            except WebSocketDisconnect:
                self._broadcaster.disconnect(ws)

        # --- Static files ---
        if static_dir and os.path.isdir(static_dir):
            self._app.mount("/", StaticFiles(directory=static_dir, html=True), name="static")

    # ------------------------------------------------------------------
    # Public API — called by sample logic classes
    # ------------------------------------------------------------------
    def on_command(self, name: str, handler: Callable[[Any], None]) -> None:
        """Register a handler for commands sent from the browser via WebSocket.

        The browser sends: ``{"command": "<name>", "value": <json_value>}``
        """
        self._command_handlers[name] = handler

    def send_to_clients(self, data: dict) -> None:
        """Push arbitrary JSON to all connected browser clients (thread-safe)."""
        self._broadcaster.broadcast_sync(data)

    def update_chart(self, **series_data: float) -> None:
        """Convenience: push chart data points.

        Sends ``{"type": "chart", "data": {...}}`` to clients.
        """
        self.send_to_clients({"type": "chart", "data": series_data})

    def log_message(self, message: str) -> None:
        """Push a log message to connected clients."""
        self.send_to_clients({"type": "log", "message": message})

    def set_status(self, status: str) -> None:
        """Push a status bar update to connected clients."""
        self.send_to_clients({"type": "status", "message": status})

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def start(self, block: bool = True) -> None:
        """Start the web server.

        If *block* is True (default), blocks the calling thread until Ctrl+C
        or :meth:`stop` is called. If False, runs in a daemon thread.
        """
        config = uvicorn.Config(
            self._app,
            host="0.0.0.0",
            port=self.port,
            log_level="warning",
        )
        self._server = uvicorn.Server(config)

        if block:
            self._run_server()
        else:
            self._thread = threading.Thread(target=self._run_server, daemon=True, name="WebForm")
            self._thread.start()

    def stop(self) -> None:
        if self._server is not None:
            self._server.should_exit = True

    def _run_server(self) -> None:
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        self._broadcaster._loop = loop
        loop.run_until_complete(self._server.serve())  # type: ignore[union-attr]
