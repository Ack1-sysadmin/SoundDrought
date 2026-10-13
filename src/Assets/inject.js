// Injected into soundcloud.com: reports player state to the host and executes host commands.
(() => {
  if (window.top !== window || !window.chrome?.webview) return;

  const host = window.chrome.webview;
  const q = sel => document.querySelector(sel);
  const text = el => (el?.getAttribute('title') || el?.textContent || '').trim();

  const clicks = {
    toggle: '.playControls__play',
    next: '.skipControl__next',
    prev: '.skipControl__previous',
    like: '.playbackSoundBadge__like',
  };

  function artwork() {
    const span = q('.playbackSoundBadge__avatar .sc-artwork span');
    const match = span && getComputedStyle(span).backgroundImage.match(/url\("?(.*?)"?\)/);
    return match ? match[1].replace(/-t\d+x\d+\./, '-t300x300.') : '';
  }

  function readState() {
    const title = q('.playbackSoundBadge__titleLink');
    const artist = q('.playbackSoundBadge__lightLink');
    const like = q('.playbackSoundBadge__like');
    const timeline = q('.playbackTimeline__progressWrapper');
    return {
      type: 'state',
      hasTrack: !!title,
      playing: !!q('.playControls__play.playing'),
      title: text(title?.querySelector('span[aria-hidden]') ?? title),
      artist: text(artist),
      liked: !!like?.classList.contains('sc-button-selected'),
      artwork: artwork(),
      url: title ? title.href.split('?')[0] : '',
      artistUrl: artist ? artist.href.split('?')[0] : '',
      position: Number(timeline?.getAttribute('aria-valuenow')) || 0,
      duration: Number(timeline?.getAttribute('aria-valuemax')) || 0,
    };
  }

  let last = '';
  function report() {
    const state = readState();
    const json = JSON.stringify(state);
    if (json !== last) {
      last = json;
      host.postMessage(state);
    }
  }
  setInterval(report, 500);

  function seek(fraction) {
    const bar = q('.playbackTimeline__progressWrapper');
    if (!bar) return;
    const r = bar.getBoundingClientRect();
    const init = { bubbles: true, button: 0, clientX: r.left + r.width * fraction, clientY: r.top + r.height / 2 };
    for (const type of ['mousedown', 'mouseup', 'click']) bar.dispatchEvent(new MouseEvent(type, init));
  }

  host.addEventListener('message', e => {
    const cmd = String(e.data);
    if (cmd.startsWith('seek:')) seek(Math.min(1, Math.max(0, Number(cmd.slice(5)))));
    else if (clicks[cmd]) q(clicks[cmd])?.click();
    setTimeout(report, 150);
  });
})();
