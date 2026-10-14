(() => {
  const thread = document.getElementById('conversation-thread');
  const form = document.getElementById('message-form');
  const csrf = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (!thread || !form || !csrf || !window.signalR) return;

  const conversationId = thread.dataset.conversationId;
  const currentUserId = thread.dataset.currentUserId;
  const status = document.getElementById('connection-status');
  const error = document.getElementById('message-error');
  const textarea = form.querySelector('textarea[name="body"]');
  const submit = form.querySelector('button[type="submit"]');
  const url = `/api/v1/messages/${encodeURIComponent(conversationId)}`;
  const seen = new Set(Array.from(thread.querySelectorAll('[data-message-id]'), node => node.dataset.messageId));

  const setStatus = message => { status.textContent = message; };
  const showError = message => { error.textContent = message; error.hidden = !message; };

  function append(message) {
    if (!message?.id || seen.has(message.id)) return;
    seen.add(message.id);
    document.getElementById('empty-conversation')?.remove();

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

  async function synchronize() {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error('Không tải được các tin nhắn mới.');
    const details = await response.json();
    for (const message of details.messages) append(message);
    window.dispatchEvent(new Event('coco:unread-changed'));
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/conversations')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

  connection.on('MessageReceived', message => {
    append(message);
    if (message.senderId !== currentUserId)
      synchronize().catch(() => setStatus('Tin mới đã tới, nhưng chưa cập nhật được trạng thái đã đọc.'));
  });
  connection.onreconnecting(() => setStatus('Đang kết nối lại…'));
  connection.onreconnected(async () => {
    try {
      await connection.invoke('JoinConversation', conversationId);
      await synchronize();
      setStatus('Tin nhắn trực tiếp đã kết nối');
    } catch {
      setStatus('Chưa đồng bộ được tin nhắn. Hãy tải lại trang.');
    }
  });
  connection.onclose(() => {
    setStatus('Mất kết nối trực tiếp. Bạn vẫn có thể gửi tin nhắn.');
    window.setTimeout(connect, 5000);
  });

  async function connect() {
    if (connection.state !== signalR.HubConnectionState.Disconnected) return;
    setStatus('Đang kết nối tin nhắn trực tiếp…');
    try {
      await connection.start();
      await connection.invoke('JoinConversation', conversationId);
      await synchronize();
      setStatus('Tin nhắn trực tiếp đã kết nối');
    } catch {
      setStatus('Chưa kết nối được tin nhắn trực tiếp; đang thử lại.');
      if (connection.state === signalR.HubConnectionState.Connected) await connection.stop();
      else window.setTimeout(connect, 5000);
    }
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submit.disabled) return;
    const body = textarea.value.trim();
    if (!body) return;
    submit.disabled = true;
    textarea.disabled = true;
    showError('');
    try {
      const response = await fetch(`${url}/messages`, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
        body: JSON.stringify({ body })
      });
      if (!response.ok) {
        const result = await response.json().catch(() => ({}));
        throw new Error(result.error || 'Không gửi được tin nhắn. Vui lòng thử lại.');
      }
      append(await response.json());
      textarea.value = '';
      textarea.focus();
    } catch (cause) {
      showError(cause.message || 'Không gửi được tin nhắn. Vui lòng thử lại.');
    } finally {
      submit.disabled = false;
      textarea.disabled = false;
    }
  });

  thread.scrollTop = thread.scrollHeight;
  connect();
})();
