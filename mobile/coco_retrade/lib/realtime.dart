import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'api.dart';

// Small SignalR JSON-protocol client for the two existing server hubs.
// Server remains authoritative: all messages are sent through the protected API.
class CoCoRealtime {
  CoCoRealtime(this.api, this.hub, this.joinMethod, this.resourceId,
      this.eventName, this.onChanged);

  final CoCoApi api;
  final String hub;
  final String joinMethod;
  final String resourceId;
  final String eventName;
  final void Function() onChanged;
  WebSocket? _socket;
  Timer? _ping;
  Timer? _retry;
  bool _closed = false;

  Future<void> connect() async {
    if (_closed) return;
    try {
      final negotiate = asMap(await api.post('$hub/negotiate?negotiateVersion=1'));
      final token = str(negotiate['connectionToken']);
      if (token.isEmpty) throw const FormatException('Missing SignalR connection token');
      final base = Uri.parse(api.baseUrl);
      final uri = base.replace(scheme: base.scheme == 'https' ? 'wss' : 'ws',
          path: hub, queryParameters: {'id': token});
      final ws = await WebSocket.connect(uri.toString(), headers: api.imageHeaders);
      if (_closed) { await ws.close(); return; }
      _socket = ws;
      ws.add('${jsonEncode({'protocol': 'json', 'version': 1})}\u001e');
      ws.listen((event) {
        if (event is! String) return;
        for (final frame in event.split('\u001e')) {
          if (frame.isEmpty) continue;
          try {
            final message = asMap(jsonDecode(frame));
            if (message.isEmpty) {
              ws.add('${jsonEncode({'type': 1, 'target': joinMethod,
                  'arguments': [resourceId], 'invocationId': '1'})}\u001e');
              onChanged();
            } else if (message['type'] == 1 && message['target'] == eventName) {
              onChanged();
            }
          } catch (_) { /* Ignore an unsupported server frame. */ }
        }
      }, onDone: _reconnect, onError: (_) => _reconnect(), cancelOnError: true);
      _ping?.cancel();
      _ping = Timer.periodic(const Duration(seconds: 15), (_) {
        if (ws.readyState == WebSocket.open) ws.add('{"type":6}\u001e');
      });
    } catch (_) { _reconnect(); }
  }

  void _reconnect() {
    _ping?.cancel();
    _socket = null;
    if (!_closed) {
      _retry?.cancel();
      _retry = Timer(const Duration(seconds: 3), connect);
    }
  }

  void dispose() {
    _closed = true;
    _ping?.cancel();
    _retry?.cancel();
    _socket?.close();
  }
}
