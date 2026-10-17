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

    /* "Play shuffled" buttons added by SoundDrought */
    .sd-shuffle {
      display: inline-flex; align-items: center; justify-content: center; gap: 8px;
      border: 0; cursor: pointer; font: 600 14px/1 inherit; color: #121212; background: #f2f2f2;
      transition: transform .1s ease, background .15s ease;
    }
    .sd-shuffle:hover { background: #fff; transform: scale(1.04); }
    .sd-shuffle svg { width: 20px; height: 20px; fill: currentColor; flex: none; }
    .sd-shuffle--round { width: 44px; height: 44px; border-radius: 50%; margin-left: 12px; vertical-align: middle; }
    .sd-shuffle--pill { height: 36px; padding: 0 16px; border-radius: 18px; margin: 0 0 16px; }
    .soundTitle__playButtonHero, .fullHero__titleButton { width: auto !important; display: flex !important; align-items: center; }
    .soundTitle__playButtonHero > *, .fullHero__titleButton > * { flex: none !important; }
    .sd-shuffle--round { margin-right: 12px; }
  `;
  const addStyle = () => (document.head || document.documentElement).appendChild(style);
  if (document.documentElement) addStyle();
  else document.addEventListener('readystatechange', addStyle, { once: true });

  const clicks = {
    toggle: '.playControls__play',
    next: '.skipControl__next',
    prev: '.skipControl__previous',
    like: '.playbackSoundBadge__like',
    mute: '.volume__speakerIcon',
  };

  const volume = () => Number(q('.volume__sliderWrapper')?.getAttribute('aria-valuenow')) || 0;

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
      volume: volume(),
      muted: !!q('.volume.muted'),
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
  setInterval(() => { report(); addShuffleButtons(); }, 500);

  // ---- "Play shuffled" for playlists and likes ----

  const shuffleLabel = navigator.language.startsWith('de') ? 'Zufallswiedergabe' : 'Play shuffled';
  const shuffleIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M10.59 9.17 5.41 4 4 5.41l5.17 5.17 1.42-1.41zM14.5 4l2.04 2.04L4 18.59 5.41 20 17.96 7.46 20 9.5V4h-5.5zm.33 9.41-1.41 1.41 3.13 3.13L14.5 20H20v-5.5l-2.04 2.04-3.13-3.13z"/></svg>'; // Material Icons "shuffle", Apache 2.0
  const sleep = ms => new Promise(r => setTimeout(r, ms));

  function makeShuffleButton(kind, getPlayButton) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `sd-shuffle sd-shuffle--${kind}`;
    button.title = shuffleLabel;
    button.setAttribute('aria-label', shuffleLabel);
    button.innerHTML = shuffleIcon + (kind === 'pill' ? `<span>${shuffleLabel}</span>` : '');
    button.addEventListener('click', e => {
      e.preventDefault();
      e.stopPropagation();
      playShuffled(getPlayButton());
    });
    return button;
  }

  // Starts the list, turns on SoundCloud's shuffle and skips ahead, so the first track is random too.
  async function playShuffled(playButton) {
    if (!playButton) return;
    const alreadyPlaying = playButton.classList.contains('sc-button-pause');
    if (!alreadyPlaying) {
      playButton.click();
      for (let i = 0; i < 40 && !q('.playControls__play.playing'); i++) await sleep(100);
    }
    const shuffle = q('.shuffleControl');
    if (shuffle && !shuffle.classList.contains('m-shuffling')) shuffle.click();
    await sleep(150);
    q('.skipControl__next')?.click();
    setTimeout(report, 300);
  }

  function addShuffleButtons() {
    // Playlists, albums and SoundCloud's own playlists/stations: next to the big play button.
    const heroPlay = q('.soundTitle__playButtonHero .sc-button-play, .fullHero__titleButton .sc-button-play');
    if (heroPlay && !heroPlay.parentElement.querySelector('.sd-shuffle')) {
      heroPlay.insertAdjacentElement('afterend', makeShuffleButton('round', () => heroPlay));
    }

    // Likes (your own and other people's): a button above the list, playing it from its first track.
    if (/^\/[^/]+\/likes\/?$/.test(location.pathname) && !q('.sd-shuffle--pill')) {
      const firstPlay = [...document.querySelectorAll('.l-main .sc-button-play, .l-content .sc-button-play')]
        .find(b => !b.closest('.playControls') && b.getBoundingClientRect().width > 0);
      const list = firstPlay?.closest('.lazyLoadingList, .badgeList, .soundList, ul');
      if (list) {
        const firstButtonOf = () => [...list.querySelectorAll('.sc-button-play')].find(b => !b.closest('.playControls'));
        list.insertAdjacentElement('beforebegin', makeShuffleButton('pill', firstButtonOf));
      }
    }
  }

  function seek(fraction) {
    const bar = q('.playbackTimeline__progressWrapper');
    if (!bar) return;
    const r = bar.getBoundingClientRect();
    // iew makes the browser fill in pageX/pageY (which include the scroll offset) like for real clicks.
    const init = { view: window, bubbles: true, button: 0, clientX: r.left + r.width * fraction, clientY: r.top + r.height / 2 };
    for (const type of ['mousedown', 'mouseup', 'click']) bar.dispatchEvent(new MouseEvent(type, init));
  }

  // Drives SoundCloud's own (hidden) volume slider, so its UI and saved setting stay in sync.
  function setVolume(value) {
    const slider = q('.volume__sliderWrapper');
    const track = q('.volume__sliderBackground')?.getBoundingClientRect();
    if (!slider || !track?.height) return;
    const target = Math.min(1, Math.max(0, value));
    const click = v => {
      // The slider measures from its handle's center, ~3% of the track below the click point.
      // Without iew, pageY would ignore the scroll offset and SoundCloud would read every click as 100%.
      const init = { view: window, bubbles: true, button: 0, buttons: 1, clientX: track.left + track.width / 2,
                     clientY: track.bottom - track.height * v + track.height * 0.03 };
      slider.dispatchEvent(new MouseEvent('mousedown', init));
      document.dispatchEvent(new MouseEvent('mouseup', init));
    };
    // The track is only ~120px tall, so a click can land a pixel off; correct until it's exact.
    let aim = target;
    for (let i = 0; i < 3; i++) {
      click(aim);
      const error = target - volume();
      if (Math.abs(error) < 0.006) break;
      aim += error;
    }
  }

  // Steps land on a clean 5% grid (35, 40, 45 …) instead of drifting.
  const stepVolume = step => setVolume(Math.round((volume() + step) * 20) / 20);

  // Small "42 %" bubble above the speaker icon while the wheel changes the volume.
  let bubble, bubbleTimer;
  function showVolume() {
    const anchor = q('.playControls__volume')?.getBoundingClientRect();
    if (!anchor) return;
    if (!bubble) {
      bubble = document.createElement('div');
      bubble.style.cssText = 'position:fixed;z-index:100000;padding:4px 10px;border-radius:12px;background:#f2f2f2;' +
        'color:#121212;font:600 12px/1.4 system-ui,sans-serif;pointer-events:none;transition:opacity .2s;transform:translateX(-50%)';
      document.body.appendChild(bubble);
    }
    bubble.textContent = q('.volume.muted') ? '🔇' : `${Math.round(volume() * 100)} %`;
    bubble.style.left = `${anchor.left + anchor.width / 2}px`;
    bubble.style.top = `${anchor.top - 30}px`;
    bubble.style.opacity = '1';
    clearTimeout(bubbleTimer);
    bubbleTimer = setTimeout(() => (bubble.style.opacity = '0'), 900);
  }

  // Mouse wheel over the player bar changes the volume in 5% steps. The "Next up" queue and the
  // upsell panel live inside the player bar too; they must keep scrolling normally.
  let wheel = 0;
  document.addEventListener('wheel', e => {
    if (!(e.target instanceof Element) || !e.target.closest('.playControls')) return;
    if (e.target.closest('.playControls__queue, .playControls__panel')) return;
    e.preventDefault();
    wheel += e.deltaY;
    if (Math.abs(wheel) < 40) return; // touchpads send many small deltas
    stepVolume(wheel < 0 ? 0.05 : -0.05);
    wheel = 0;
    setTimeout(() => { showVolume(); report(); }, 30);
  }, { passive: false, capture: true });

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
    else if (cmd.startsWith('volume:')) setVolume(Number(cmd.slice(7)));
    else if (cmd.startsWith('volumeBy:')) stepVolume(Number(cmd.slice(9)));
    else if (cmd === 'signin') signIn();
    else if (cmd === 'back') history.back();
    else if (cmd === 'forward') history.forward();
    else if (clicks[cmd]) q(clicks[cmd])?.click();
    setTimeout(report, 150);
  });
})();
