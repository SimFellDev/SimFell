// Sizes a review iframe to its page, so the app page scrolls instead of the frame. A page from another origin (the
// desktop app's review host) keeps the frame's own height and scroll bar.
export function fit(frame) {
  let observer = null;
  const size = () => {
    observer?.disconnect();
    const body = frame.contentDocument?.body;
    if (!body || frame.contentWindow.location.href === 'about:blank') {
      frame.classList.remove('fitted');
      return;
    }
    const set = () => { frame.style.height = `${Math.ceil(body.getBoundingClientRect().height)}px`; };
    frame.classList.add('fitted');
    set();
    observer = new ResizeObserver(set);
    observer.observe(body);
  };
  frame.addEventListener('load', size);
  if (frame.contentDocument?.readyState === 'complete') size();
  return {
    dispose() {
      observer?.disconnect();
      frame.removeEventListener('load', size);
    },
  };
}
