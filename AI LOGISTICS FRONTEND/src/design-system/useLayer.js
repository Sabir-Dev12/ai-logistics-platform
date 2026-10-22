import { useEffect, useRef } from "react";

const focusable = "button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[tabindex]:not([tabindex='-1'])";
const stacks = new WeakMap();
const scrollLocks = new WeakMap();

export function useLayer({ open, onDismiss, rootRef, triggerRef, modal = false }) {
  const callback = useRef(onDismiss);
  callback.current = onDismiss;
  useEffect(() => {
    if (!open || !rootRef.current) return;
    const root = rootRef.current;
    const doc = root.ownerDocument;
    const previous = doc.activeElement;
    const layer = {};
    const stack = stacks.get(doc) || [];
    stacks.set(doc, stack);
    stack.push(layer);
    if (modal) {
      const lock = scrollLocks.get(doc) || { count: 0, overflow: doc.body.style.overflow };
      lock.count += 1;
      scrollLocks.set(doc, lock);
      doc.body.style.overflow = "hidden";
    }
    (root.querySelector(focusable) || root).focus();
    const topmost = () => stack.at(-1) === layer;
    const keydown = (event) => {
      if (!topmost()) return;
      if (event.key === "Escape") { event.preventDefault(); callback.current?.(); }
      if (modal && event.key === "Tab") {
        const items = [...root.querySelectorAll(focusable)].filter((item) => !item.hidden && item.getAttribute("aria-hidden") !== "true");
        const first = items[0];
        const last = items.at(-1);
        if (!first) { event.preventDefault(); root.focus(); }
        else if (event.shiftKey && (doc.activeElement === first || doc.activeElement === root || !root.contains(doc.activeElement))) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && (doc.activeElement === last || !root.contains(doc.activeElement))) { event.preventDefault(); first.focus(); }
      }
    };
    const pointerdown = (event) => {
      if (topmost() && !root.contains(event.target) && !triggerRef?.current?.contains(event.target)) callback.current?.();
    };
    doc.addEventListener("keydown", keydown);
    doc.addEventListener("pointerdown", pointerdown);
    return () => {
      const wasTopmost = topmost();
      stack.splice(stack.indexOf(layer), 1);
      doc.removeEventListener("keydown", keydown);
      doc.removeEventListener("pointerdown", pointerdown);
      if (modal) {
        const lock = scrollLocks.get(doc);
        lock.count -= 1;
        if (!lock.count) { doc.body.style.overflow = lock.overflow; scrollLocks.delete(doc); }
      }
      if (wasTopmost && previous?.isConnected) previous.focus();
    };
  }, [open, modal, rootRef, triggerRef]);
}
