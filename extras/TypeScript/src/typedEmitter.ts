/** Small zero-dependency typed event emitter. */
export class TypedEmitter<TEvents extends { [K in keyof TEvents]: (...args: never[]) => void }> {
  private readonly handlers = new Map<keyof TEvents, Set<TEvents[keyof TEvents]>>();

  on<K extends keyof TEvents>(event: K, handler: TEvents[K]): this {
    let eventHandlers = this.handlers.get(event);
    if (eventHandlers === undefined) {
      eventHandlers = new Set<TEvents[keyof TEvents]>();
      this.handlers.set(event, eventHandlers);
    }
    eventHandlers.add(handler);
    return this;
  }

  off<K extends keyof TEvents>(event: K, handler: TEvents[K]): this {
    this.handlers.get(event)?.delete(handler);
    return this;
  }

  once<K extends keyof TEvents>(event: K, handler: TEvents[K]): this {
    const onceHandler = ((...args: Parameters<TEvents[K]>) => {
      this.off(event, onceHandler as TEvents[K]);
      handler(...args);
    }) as TEvents[K];
    return this.on(event, onceHandler);
  }

  emit<K extends keyof TEvents>(event: K, ...args: Parameters<TEvents[K]>): void {
    const eventHandlers = this.handlers.get(event);
    if (eventHandlers === undefined) {
      return;
    }
    for (const handler of [...eventHandlers]) {
      handler(...(args as never[]));
    }
  }

  clear(event?: keyof TEvents): void {
    if (event === undefined) {
      this.handlers.clear();
      return;
    }
    this.handlers.delete(event);
  }
}
