# CmdMessenger TypeScript

Node-first TypeScript port of the CmdMessenger host-side library.

Status: initial core implementation. The protocol core, command model, queues,
ACK flow, loopback transport, and Node serial/TCP transports are implemented.

```ts
import { BoardType, CmdMessenger, SendCommand } from 'cmd-messenger';
import { LoopbackTransport } from 'cmd-messenger/transport/loopback';

const transport = new LoopbackTransport();
const messenger = new CmdMessenger(transport, BoardType.Bit16);

await messenger.connect();
await messenger.sendCommand(new SendCommand(0, 'hello'));
```

Text arguments are not automatically escaped. Use `escape(...)` explicitly when
the receiving side expects escaped payload text, matching the existing C# and
Python ports.

## Tests

```sh
npm test
npm run test:hardware
```

`npm run test:hardware` requires a board running
`test/integration/sketch/src/LoopbackTestRunner.ino`. It uses
`CMDMSG_HW_PORT`/`CMDMESSENGER_PORT` when set; otherwise it tries to discover
provisioned boards and falls back to the only available serial port.
