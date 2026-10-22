document.querySelectorAll('input[data-preview]').forEach(input => {
  let selected = [];
  const target = document.getElementById(input.dataset.preview);
  if (!target) return;
  const maxFiles = Number(input.dataset.maxFiles) || 10;
  const existing = input.closest('form')?.querySelector('#existingPhotos');
  const existingCount = () => existing?.querySelectorAll('[data-existing-photo]:not([hidden])').length || 0;
  function render() {
    const transfer = new DataTransfer();
    selected.forEach(file => transfer.items.add(file));
    input.files = transfer.files;
    target.replaceChildren();
    selected.forEach((file, index) => {
      const item = document.createElement('div');
      item.className = 'photo-preview-item';
      const image = document.createElement('img');
      image.alt = file.name;
      image.src = URL.createObjectURL(file);
      image.onload = () => URL.revokeObjectURL(image.src);
      const remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'photo-preview-remove';
      remove.setAttribute('aria-label', `Xóa ảnh ${file.name}`);
      remove.textContent = '×';
      remove.addEventListener('click', () => { selected.splice(index, 1); render(); });
      item.append(image, remove);
      target.append(item);
    });
  }
  input.addEventListener('change', () => {
    const incoming = Array.from(input.files);
    for (const file of incoming) {
      if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) continue;
      if (selected.length + existingCount() >= maxFiles) {
        window.alert(`Chỉ được chọn tối đa ${maxFiles} ảnh.`);
        break;
      }
      if (!selected.some(current => current.name === file.name && current.size === file.size && current.lastModified === file.lastModified))
        selected.push(file);
    }
    render();
  });
  existing?.querySelectorAll('[data-remove-existing]').forEach(button => {
    button.addEventListener('click', () => {
      const item = button.closest('[data-existing-photo]');
      item.hidden = true;
      item.querySelector('input[name="RetainedImageIds"]').disabled = true;
      render();
    });
  });
});

(() => {
  const badges = document.querySelectorAll('[data-cart-count]');
  let cartUpdated = false;
  function showCount(count) {
    for (const badge of badges) {
      badge.hidden = count < 1;
      badge.textContent = count > 99 ? '99+' : String(count);
      badge.closest('.cart-link')?.setAttribute('aria-label', `Giỏ hàng: ${count} món`);
    }
  }
  if (badges.length) {
    fetch('/Cart/Count', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } })
      .then(response => response.ok ? response.json() : null)
      .then(data => { if (data && !cartUpdated) showCount(data.count); })
      .catch(() => {});
  }
  let feedbackTimer;
  function showFeedback(message, error = false) {
    let feedback = document.getElementById('cartFeedback');
    if (!feedback) {
      feedback = document.createElement('div');
      feedback.id = 'cartFeedback';
      feedback.className = 'cart-feedback';
      feedback.setAttribute('role', 'status');
      feedback.setAttribute('aria-live', 'polite');
      document.body.append(feedback);
    }
    feedback.textContent = message;
    feedback.classList.toggle('is-error', error);
    feedback.hidden = false;
    clearTimeout(feedbackTimer);
    feedbackTimer = setTimeout(() => { feedback.hidden = true; }, 3000);
  }
  document.querySelectorAll('form[data-cart-add]').forEach(form => form.addEventListener('submit', async event => {
    event.preventDefault();
    const button = form.querySelector('button[type="submit"]');
    if (button.disabled) return;
    const original = button.innerHTML;
    button.disabled = true;
    try {
      const response = await fetch(form.action, {
        method: 'POST', body: new FormData(form), credentials: 'same-origin',
        headers: { Accept: 'application/json' }
      });
      if (!response.headers.get('content-type')?.includes('application/json')) {
        if (response.redirected) window.location.href = response.url;
        else showFeedback('Không thêm được món đồ. Vui lòng thử lại.', true);
        return;
      }
      const result = await response.json();
      if (!response.ok) { showFeedback(result.error || 'Không thêm được món đồ.', true); return; }
      cartUpdated = true;
      showCount(result.count);
      showFeedback(result.message || 'Đã thêm vào giỏ hàng.');
      button.textContent = '✓ Đã thêm';
      setTimeout(() => { button.innerHTML = original; }, 1700);
    } catch { showFeedback('Không kết nối được. Vui lòng thử lại.', true); }
    finally { button.disabled = false; }
  }));
})();

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
