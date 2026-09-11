export class Signal<T> {
  private listeners = new Set<(value: T) => void>();
  subscribe = (listener: (value: T) => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  emit(value: T) {
    for (const listener of this.listeners) listener(value);
  }
  clear() {
    this.listeners.clear();
  }
}
