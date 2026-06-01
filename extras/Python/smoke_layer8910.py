"""Layer 8 / 9 / 10 smoke tests — Logger, TcpTransport, ConnectionManager."""
from __future__ import annotations

import os
import socket
import tempfile
import threading
import time

import cmd_messenger as cm


# ----------------------------------------------------------------------
# Layer 10: Logger
# ----------------------------------------------------------------------
def t1_logger():
    print("[1] Logger writes lines to file")
    fd, path = tempfile.mkstemp(prefix="cmlog_", suffix=".log")
    os.close(fd)
    try:
        assert cm.logger.open(path)
        assert cm.logger.is_open()
        cm.logger.log_line("hello world")
        cm.logger.log_line("second line")
        cm.logger.close()
        assert not cm.logger.is_open()
        with open(path, encoding="iso-8859-1") as f:
            content = f.read()
        assert "hello world" in content and "second line" in content, content
        # set_enabled gating
        cm.logger.set_enabled(False)
        cm.logger.open(path)
        cm.logger.log_line("ignored")
        cm.logger.close()
        cm.logger.set_enabled(True)
        with open(path, encoding="iso-8859-1") as f:
            content2 = f.read()
        assert "ignored" not in content2
        print("   ok")
    finally:
        try:
            os.unlink(path)
        except OSError:
            pass


# ----------------------------------------------------------------------
# Helper: mini Arduino-like TCP echo server that answers an "identify" challenge
# ----------------------------------------------------------------------
class FakeArduinoTcp:
    """Single-client server that answers commands.

    On receiving cmd id ``identify_id`` it writes ``f"{identify_id},{unique_id};"``.
    Other commands are silently dropped (or echoed for non-ack ones if echo=True).
    """

    def __init__(self, identify_id: int = 1, unique_id: str = "FAKE-001") -> None:
        self.identify_id = identify_id
        self.unique_id = unique_id
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._sock.bind(("127.0.0.1", 0))
        self._sock.listen(1)
        self.port = self._sock.getsockname()[1]
        self._stop = False
        self._thread = threading.Thread(target=self._run, daemon=True, name="FakeArduino")
        self._thread.start()

    def stop(self) -> None:
        self._stop = True
        try:
            self._sock.close()
        except OSError:
            pass

    def _run(self) -> None:
        try:
            self._sock.settimeout(0.2)
            while not self._stop:
                try:
                    conn, _addr = self._sock.accept()
                except (socket.timeout, OSError):
                    continue
                self._handle(conn)
        finally:
            try:
                self._sock.close()
            except OSError:
                pass

    def _handle(self, conn: socket.socket) -> None:
        conn.settimeout(0.1)
        buf = bytearray()
        try:
            while not self._stop:
                try:
                    chunk = conn.recv(4096)
                except socket.timeout:
                    continue
                except OSError:
                    return
                if not chunk:
                    return
                buf.extend(chunk)
                while b";" in buf:
                    line, _, rest = buf.partition(b";")
                    buf = bytearray(rest)
                    fields = line.decode("iso-8859-1").split(",")
                    if not fields:
                        continue
                    try:
                        cmd_id = int(fields[0])
                    except ValueError:
                        continue
                    if cmd_id == self.identify_id:
                        reply = f"{self.identify_id},{self.unique_id};"
                        try:
                            conn.sendall(reply.encode("iso-8859-1"))
                        except OSError:
                            return
        finally:
            try:
                conn.close()
            except OSError:
                pass


# ----------------------------------------------------------------------
# Layer 9: TcpTransport end-to-end
# ----------------------------------------------------------------------
def t2_tcp_transport():
    print("[2] TcpTransport round-trip via FakeArduinoTcp")
    server = FakeArduinoTcp(identify_id=7, unique_id="DEVICE-XYZ")
    try:
        transport = cm.TcpTransport("127.0.0.1", server.port, timeout_ms=500)
        messenger = cm.CmdMessenger(transport, board_type=cm.BoardType.BIT_32)
        assert messenger.connect()
        replies: list[cm.ReceivedCommand] = []
        messenger.attach(7, replies.append)

        # Fire a non-ack identify; server echoes "7,DEVICE-XYZ;"
        messenger.send_command(cm.SendCommand(7))
        deadline = time.time() + 2.0
        while time.time() < deadline and not replies:
            time.sleep(0.01)
        assert replies, "no reply received"
        assert replies[0].cmd_id == 7
        assert replies[0].read_string_arg() == "DEVICE-XYZ"
        messenger.disconnect()
        messenger.dispose()
        print("   ok")
    finally:
        server.stop()


# ----------------------------------------------------------------------
# Layer 8: ConnectionManager (via TcpConnectionManager) — find device + watchdog
# ----------------------------------------------------------------------
def t3_tcp_connection_manager():
    print("[3] TcpConnectionManager triggers connection_found event")
    server = FakeArduinoTcp(identify_id=9, unique_id="UID-9")
    try:
        transport = cm.TcpTransport("127.0.0.1", server.port, timeout_ms=500)
        messenger = cm.CmdMessenger(transport, board_type=cm.BoardType.BIT_32)
        connmgr = cm.TcpConnectionManager(
            transport, messenger, watchdog_command_id=9, unique_device_id="UID-9"
        )
        events: list[str] = []
        connmgr.connection_found += lambda: events.append("found")
        connmgr.connection_timeout += lambda: events.append("timeout")
        progress: list[cm.ConnectionManagerProgressEventArgs] = []
        connmgr.progress += progress.append

        connmgr.start_connection_manager()
        deadline = time.time() + 5.0
        while time.time() < deadline and "found" not in events:
            time.sleep(0.05)
        assert "found" in events, f"never found. events={events} progress={[p.description for p in progress]}"
        assert connmgr.connected
        connmgr.stop_connection_manager()
        messenger.dispose()
        print(f"   ok (events={events})")
    finally:
        server.stop()


def t4_tcp_connection_manager_failure():
    print("[4] TcpConnectionManager handles missing endpoint without crash")
    # Bind & immediately close to grab a guaranteed-free port.
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    free_port = s.getsockname()[1]
    s.close()

    transport = cm.TcpTransport("127.0.0.1", free_port, timeout_ms=200)
    messenger = cm.CmdMessenger(transport, board_type=cm.BoardType.BIT_32)
    connmgr = cm.TcpConnectionManager(transport, messenger, watchdog_command_id=1)
    events: list[str] = []
    connmgr.connection_found += lambda: events.append("found")
    connmgr.start_connection_manager()
    time.sleep(0.5)
    connmgr.stop_connection_manager()
    messenger.dispose()
    assert "found" not in events
    print("   ok (gracefully gave up)")


if __name__ == "__main__":
    t1_logger()
    t2_tcp_transport()
    t3_tcp_connection_manager()
    t4_tcp_connection_manager_failure()
    print("\nAll Layer 8/9/10 smoke tests passed.")
