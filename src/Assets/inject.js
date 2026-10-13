// Injected into soundcloud.com: hides SoundCloud's header (the app draws its own), reports player
// and account state to the host and executes host commands.
(() => {
  if (window.top !== window || !window.chrome?.webview) return;

  const host = window.chrome.webview;
  const q = sel => document.querySelector(sel);
  const text = el => (el?.getAttribute('title') || el?.textContent || '').trim();
  const bgImage = el => el && getComputedStyle(el).backgroundImage.match(/url\("?(.*?)"?\)/)?.[1] || '';
  const path = a => a ? a.href.split('?')[0] : '';

  const style = document.createElement('style');
  // SoundCloud pins its layout to 960px on narrow windows; let it flow instead so nothing gets cut off.
  style.textContent = `
    header.header { display: none !important; }
    .l-container.l-content { padding-top: 0 !important; }
    .dropbar { top: 0 !important; }

    @media (max-width: 1000px) {
      .l-container, .dropbar__content { width: auto !important; min-width: 0 !important; }
      .dropbar { width: auto !important; left: 0 !important; right: 0 !important; }
      .l-fluid-fixed .l-main, .l-main { width: auto !important; float: none !important; margin-right: 0 !important; border-right: 0 !important; }
      .l-sidebar-right { display: none !important; }
    }
    @media (max-width: 820px) {
      .playControls__soundBadge { flex: 0 1 260px !important; min-width: 0 !important; margin-left: 12px !important; }
      .playControls__timeline { min-width: 0 !important; margin-left: 12px !important; }
      .userDropbar__buttons .sc-button-label { display: none !important; }
    }
    @media (max-width: 640px) {
      .playControls__shuffle, .playControls__repeat { display: none !important; }
      .playbackTimeline__timePassed, .playbackTimeline__duration { display: none !important; }
    }
    @media (max-width: 520px) {
      .playControls__timeline { display: none !important; }
    }
  `;
  const addStyle = () => (document.head || document.documentElement).appendChild(style);
  if (document.documentElement) addStyle();
  else document.addEventListener('readystatechange', addStyle, { once: true });

  const clicks = {
    toggle: '.playControls__play',
    next: '.skipControl__next',
    prev: '.skipControl__previous',
    like: '.playbackSoundBadge__like',
  };

  function readState() {
    const title = q('.playbackSoundBadge__titleLink');
    const artist = q('.playbackSoundBadge__lightLink');
    const like = q('.playbackSoundBadge__like');
    const timeline = q('.playbackTimeline__progressWrapper');
    const user = q('.header__userNavUsernameButton');
    return {
      type: 'state',
      hasTrack: !!title,
      playing: !!q('.playControls__play.playing'),
      title: text(title?.querySelector('span[aria-hidden]') ?? title),
      artist: text(artist),
      liked: !!like?.classList.contains('sc-button-selected'),
      artwork: bgImage(q('.playbackSoundBadge__avatar .sc-artwork span')).replace(/-t\d+x\d+\./, '-t300x300.'),
      url: path(title),
      artistUrl: path(artist),
      position: Number(timeline?.getAttribute('aria-valuenow')) || 0,
      duration: Number(timeline?.getAttribute('aria-valuemax')) || 0,
      loggedIn: !!user,
      userUrl: user?.getAttribute('href') || '',
      avatar: bgImage(user?.querySelector('span.sc-artwork')).replace(/-t\d+x\d+\./, '-t120x120.'),
      newNotifications: !!q('.header__userNavActivitiesButton .newItems__some'),
      newMessages: !!q('.header__userNavMessagesButton .newItems__some'),
      path: location.pathname,
      canGoBack: !!window.navigation?.canGoBack,
      canGoForward: !!window.navigation?.canGoForward,
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

  // SoundCloud routes link clicks itself, so clicking a temporary link navigates without a reload
  // and the music keeps playing.
  function navigate(href) {
    const a = document.createElement('a');
    a.href = href;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  function signIn() {
    const button = [...document.querySelectorAll('header button, header a')]
      .find(el => /login|signin/i.test(el.className));
    if (button) button.click();
    else location.href = 'https://soundcloud.com/signin';
  }

  host.addEventListener('message', e => {
    const cmd = String(e.data);
    if (cmd.startsWith('seek:')) seek(Math.min(1, Math.max(0, Number(cmd.slice(5)))));
    else if (cmd.startsWith('nav:')) navigate(cmd.slice(4));
    else if (cmd === 'signin') signIn();
    else if (cmd === 'back') history.back();
    else if (cmd === 'forward') history.forward();
    else if (clicks[cmd]) q(clicks[cmd])?.click();
    setTimeout(report, 150);
  });
})();
