import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:image_picker/image_picker.dart';

class ApiException implements Exception {
  const ApiException(this.status, this.message);
  final int status;
  final String message;
  @override
  String toString() => message;
}

typedef JsonMap = Map<String, dynamic>;

JsonMap asMap(dynamic value) => value is Map<String, dynamic> ? value : <String, dynamic>{};
List<JsonMap> asList(dynamic value) => value is List ? value.map(asMap).toList() : <JsonMap>[];
String str(dynamic value) => value?.toString() ?? '';
num number(dynamic value) => value is num ? value : num.tryParse(str(value)) ?? 0;
String money(dynamic value) {
  final digits = number(value).round().toString();
  return '${digits.replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (match) => ',')} VNĐ';
}

String newKey() {
  final bytes = List<int>.generate(16, (_) => Random.secure().nextInt(256));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  final hex = bytes.map((e) => e.toRadixString(16).padLeft(2, '0')).join();
  return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-'
      '${hex.substring(16, 20)}-${hex.substring(20)}';
}

class CoCoApi {
  CoCoApi({String? baseUrl})
      : baseUrl = (baseUrl ?? const String.fromEnvironment('COCO_API_BASE_URL',
                    defaultValue: 'http://10.0.2.2:5001'))
                .replaceAll(RegExp(r'/$'), '');

  final String baseUrl;
  final FlutterSecureStorage _store = const FlutterSecureStorage();
  final HttpClient _http = HttpClient();
  final Map<String, String> _cookies = {};
  String? _csrf;

  Future<void> init() async {
    final saved = await _store.read(key: 'coco_cookies');
    if (saved != null) {
      final parsed = jsonDecode(saved);
      if (parsed is Map) {
        for (final entry in parsed.entries) {
          _cookies[entry.key.toString()] = entry.value.toString();
        }
      }
    }
  }

  Uri url(String path, [Map<String, String>? query]) =>
      Uri.parse('$baseUrl$path').replace(queryParameters: query);

  String imageUrl(dynamic imageId) => '$baseUrl/images/$imageId';
  Map<String, String> get imageHeaders => _cookies.isEmpty ? const {} :
      {HttpHeaders.cookieHeader: _cookies.entries.map((e) => '${e.key}=${e.value}').join('; ')};

  Future<dynamic> get(String path, [Map<String, String>? query]) =>
      request('GET', path, query: query);
  Future<dynamic> post(String path, [JsonMap? body]) =>
      request('POST', path, body: body);
  Future<dynamic> put(String path, [JsonMap? body]) =>
      request('PUT', path, body: body);
  Future<dynamic> delete(String path) => request('DELETE', path);

  Future<void> _token() async {
    if (_csrf != null) return;
    final result = asMap(await request('GET', '/api/v1/session/antiforgery'));
    _csrf = str(result['token']);
  }

  Future<dynamic> request(String method, String path,
      {JsonMap? body, Map<String, String>? query}) async {
    if (method != 'GET') await _token();
    final req = await _http.openUrl(method, url(path, query));
    _decorate(req, method);
    if (body != null) {
      req.headers.contentType = ContentType.json;
      req.add(utf8.encode(jsonEncode(body)));
    }
    return _finish(req);
  }

  Future<dynamic> multipart(String path, Map<String, String> fields,
      List<XFile> files, String fileField) async {
    await _token();
    final boundary = 'coco-${newKey()}';
    final req = await _http.postUrl(url(path));
    _decorate(req, 'POST');
    req.headers.set(HttpHeaders.contentTypeHeader, 'multipart/form-data; boundary=$boundary');
    for (final field in fields.entries) {
      req.add(utf8.encode('--$boundary\r\n'
          'Content-Disposition: form-data; name="${field.key}"\r\n\r\n'
          '${field.value}\r\n'));
    }
    for (final file in files) {
      final name = file.name.replaceAll(RegExp(r'[^A-Za-z0-9._-]'), '_');
      final type = name.toLowerCase().endsWith('.png') ? 'image/png'
          : name.toLowerCase().endsWith('.webp') ? 'image/webp' : 'image/jpeg';
      req.add(utf8.encode('--$boundary\r\n'
          'Content-Disposition: form-data; name="$fileField"; filename="$name"\r\n'
          'Content-Type: $type\r\n\r\n'));
      req.add(await file.readAsBytes());
      req.add(utf8.encode('\r\n'));
    }
    req.add(utf8.encode('--$boundary--\r\n'));
    return _finish(req);
  }

  void _decorate(HttpClientRequest req, String method) {
    req.headers.set(HttpHeaders.acceptHeader, 'application/json');
    if (_cookies.isNotEmpty) {
      req.headers.set(HttpHeaders.cookieHeader,
          _cookies.entries.map((e) => '${e.key}=${e.value}').join('; '));
    }
    if (method != 'GET' && _csrf != null) {
      req.headers.set('X-CSRF-TOKEN', _csrf!);
    }
  }

  Future<dynamic> _finish(HttpClientRequest req) async {
    final response = await req.close();
    for (final cookie in response.cookies) {
      if (cookie.name == 'CoCo.Auth') _csrf = null;
      if (cookie.maxAge == 0 || cookie.value.isEmpty) {
        _cookies.remove(cookie.name);
      } else {
        _cookies[cookie.name] = cookie.value;
      }
    }
    await _store.write(key: 'coco_cookies', value: jsonEncode(_cookies));
    final raw = await utf8.decoder.bind(response).join();
    dynamic payload;
    try { payload = raw.isEmpty ? null : jsonDecode(raw); }
    catch (_) { payload = raw; }
    if (response.statusCode >= 400) {
      final data = asMap(payload);
      final message = str(data['error']).isNotEmpty ? str(data['error'])
          : str(data['title']).isNotEmpty ? str(data['title'])
          : response.statusCode == 401 ? 'Vui lòng đăng nhập.'
          : response.statusCode == 403 ? 'Bạn không có quyền thực hiện thao tác này.'
          : 'Không thể thực hiện yêu cầu (${response.statusCode}).';
      throw ApiException(response.statusCode, message);
    }
    return payload;
  }

  Future<JsonMap> login(String identifier, String password) async {
    final result = asMap(await post('/api/v1/mobile/account/login',
        {'identifier': identifier, 'password': password}));
    _csrf = null; // Identity changed; antiforgery token must be refreshed.
    return result;
  }

  Future<void> logout() async {
    await post('/api/v1/mobile/account/logout');
    _csrf = null;
    _cookies.clear();
    await _store.delete(key: 'coco_cookies');
  }
}
