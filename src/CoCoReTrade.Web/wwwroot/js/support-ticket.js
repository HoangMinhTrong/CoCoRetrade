(() => {
  const thread = document.getElementById('support-thread');
  if (!thread || !window.signalR) return;

  const ticketId = thread.dataset.ticketId;
  const apiUrl = thread.dataset.apiUrl;
  const currentUserId = thread.dataset.currentUserId;
  const form = document.getElementById('support-reply-form');
  const csrf = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;
  const textarea = form?.querySelector('textarea[name="body"]');
  const submit = form?.querySelector('button[type="submit"]');
  const error = document.getElementById('support-error');
  const status = document.getElementById('support-connection-status');
  const ticketStatus = document.getElementById('support-ticket-status');
  const seen = new Set(Array.from(thread.querySelectorAll('[data-message-id]'), node => node.dataset.messageId));

  function append(message) {
    if (!message?.id || seen.has(message.id)) return;
    seen.add(message.id);

    const row = document.createElement('div');
    row.className = `message-row ${message.senderId === currentUserId ? 'mine' : 'theirs'}`;
    row.dataset.messageId = message.id;
    const bubble = document.createElement('div');
    bubble.className = 'message-bubble';
    const body = document.createElement('p');
    body.className = 'mb-1';
    body.style.whiteSpace = 'pre-wrap';
    body.textContent = message.body;
    const time = document.createElement('small');
    time.textContent = new Date(message.createdAtUtc).toLocaleString('vi-VN', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
    });
    bubble.append(body, time);
    row.append(bubble);
    thread.append(row);
    thread.scrollTop = thread.scrollHeight;
  }

  function applyDetails(details) {
    for (const message of details.messages || []) append(message);
    if (details.ticket?.status === 'Closed') {
      ticketStatus.textContent = 'Đã đóng';
      form?.remove();
      document.getElementById('support-close-form')?.remove();
      status.textContent = 'Yêu cầu hỗ trợ đã đóng.';
    }
    window.dispatchEvent(new Event('coco:unread-changed'));
  }

  async function synchronize() {
    const response = await fetch(apiUrl, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
    });
    if (!response.ok) throw new Error('Không tải được phản hồi hỗ trợ mới.');
    applyDetails(await response.json());
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/support')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();
  connection.on('SupportTicketChanged', () => {
    synchronize().catch(() => { status.textContent = 'Chưa đồng bộ được phản hồi. Hãy tải lại trang.'; });
  });
  connection.onreconnecting(() => { status.textContent = 'Đang kết nối lại hỗ trợ trực tiếp…'; });
  connection.onreconnected(async () => {
    try {
      await connection.invoke('JoinTicket', ticketId);
      await synchronize();
      if (document.getElementById('support-reply-form')) status.textContent = 'Hỗ trợ trực tiếp đã kết nối.';
    } catch {
      status.textContent = 'Chưa đồng bộ được phản hồi. Hãy tải lại trang.';
    }
  });
  connection.onclose(() => {
    status.textContent = 'Mất kết nối trực tiếp. Bạn vẫn có thể gửi phản hồi.';
    window.setTimeout(connect, 5000);
  });

  async function connect() {
    if (connection.state !== signalR.HubConnectionState.Disconnected) return;
    try {
      await connection.start();
      await connection.invoke('JoinTicket', ticketId);
      await synchronize();
      if (document.getElementById('support-reply-form')) status.textContent = 'Hỗ trợ trực tiếp đã kết nối.';
    } catch {
      status.textContent = 'Chưa kết nối được hỗ trợ trực tiếp; đang thử lại.';
      if (connection.state === signalR.HubConnectionState.Connected) await connection.stop();
      else window.setTimeout(connect, 5000);
    }
  }

  if (form && csrf && textarea && submit && error) {
    form.addEventListener('submit', async event => {
      event.preventDefault();
      if (submit.disabled) return;
      const body = textarea.value.trim();
      if (body.length < 3) return;
      submit.disabled = true;
      textarea.disabled = true;
      error.hidden = true;
      error.textContent = '';
      try {
        const response = await fetch(`${apiUrl}/replies`, {
          method: 'POST', credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
          body: JSON.stringify({ body })
        });
        const details = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(details.error || 'Không gửi được phản hồi. Vui lòng thử lại.');
        applyDetails(details);
        textarea.value = '';
        textarea.focus();
      } catch (cause) {
        error.textContent = cause.message || 'Không gửi được phản hồi. Vui lòng thử lại.';
        error.hidden = false;
      } finally {
        submit.disabled = false;
        textarea.disabled = false;
      }
    });
  }

  thread.scrollTop = thread.scrollHeight;
  connect();
})();
