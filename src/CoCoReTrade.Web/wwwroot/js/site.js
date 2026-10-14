document.querySelectorAll('input[data-preview]').forEach(input => {
  input.addEventListener('change', () => {
    const target = document.getElementById(input.dataset.preview);
    target.replaceChildren();
    for (const file of Array.from(input.files).slice(0, 10)) {
      if (!file.type.startsWith('image/')) continue;
      const image = document.createElement('img');
      image.alt = file.name;
      image.src = URL.createObjectURL(file);
      image.onload = () => URL.revokeObjectURL(image.src);
      target.append(image);
    }
  });
});

(() => {
  const badges = document.querySelectorAll('[data-unread-badge]');
  if (!badges.length) return;

  let inFlight = false;
  async function refreshUnreadCounts() {
    if (inFlight || document.visibilityState === 'hidden') return;
    inFlight = true;
    try {
      const response = await fetch('/api/v1/unread-counts', {
        credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
      });
      if (!response.ok) return;
      const counts = await response.json();
      for (const badge of badges) {
        const type = badge.dataset.unreadBadge;
        const count = Math.max(0, Number(counts[type]) || 0);
        badge.hidden = count === 0;
        badge.textContent = count > 99 ? '99+' : String(count);
        const link = badge.closest('[data-unread-label]');
        if (link) link.setAttribute('aria-label', count > 0
          ? `${link.dataset.unreadLabel}: ${count} chưa đọc`
          : link.dataset.unreadLabel);
      }
    } catch {
      // Keep the last displayed counts until the next refresh.
    } finally {
      inFlight = false;
    }
  }

  window.addEventListener('coco:unread-changed', refreshUnreadCounts);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') refreshUnreadCounts();
  });
  window.addEventListener('focus', refreshUnreadCounts);
  window.setInterval(refreshUnreadCounts, 15000);
  refreshUnreadCounts();

  if (!window.signalR) return;
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/conversations')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();
  connection.on('UnreadCountsChanged', refreshUnreadCounts);
  connection.onreconnected(refreshUnreadCounts);
  connection.onclose(() => window.setTimeout(connect, 5000));
  async function connect() {
    if (connection.state !== signalR.HubConnectionState.Disconnected) return;
    try { await connection.start(); }
    catch { window.setTimeout(connect, 5000); }
  }
  connect();
})();
