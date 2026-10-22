import 'package:flutter/material.dart';

import 'api.dart';
import 'ui.dart';

class AuthPage extends StatefulWidget {
  const AuthPage({super.key});
  @override
  State<AuthPage> createState() => _AuthPageState();
}

class _AuthPageState extends State<AuthPage> {
  final form = GlobalKey<FormState>();
  final name = TextEditingController();
  final email = TextEditingController();
  final phone = TextEditingController();
  final identifier = TextEditingController();
  final password = TextEditingController();
  final confirm = TextEditingController();
  final code = TextEditingController();
  String step = 'login';
  String? userId;
  String? resetToken;
  bool busy = false;

  @override
  void dispose() {
    for (final c in [name, email, phone, identifier, password, confirm, code]) {
      c.dispose();
    }
    super.dispose();
  }

  void go(String target) {
    setState(() { step = target; form.currentState?.reset(); });
  }

  Future<void> submit() async {
    if (!(form.currentState?.validate() ?? false)) return;
    setState(() => busy = true);
    try {
      if (step == 'login') {
        session.value = await api.login(identifier.text.trim(), password.text);
        if (mounted && session.value?['mustChangePassword'] == true) {
          await Navigator.of(context).push(MaterialPageRoute<void>(
              builder: (_) => const ChangePasswordPage()));
        }
        if (mounted) Navigator.pop(context, true);
      } else if (step == 'register') {
        final result = asMap(await api.post('/api/v1/mobile/account/register', {
          'fullName': name.text.trim(), 'email': email.text.trim(),
          'phone': phone.text.trim(), 'password': password.text,
        }));
        userId = str(result['userId']);
        if (mounted) { flash(context, str(result['message'])); go('activate'); }
      } else if (step == 'resend') {
        final result = asMap(await api.post('/api/v1/mobile/account/resend-activation',
            {'identifier': identifier.text.trim()}));
        userId = str(result['userId']);
        if (mounted) { flash(context, str(result['message'])); go('activate'); }
      } else if (step == 'activate') {
        await api.post('/api/v1/mobile/account/activate',
            {'userId': userId, 'code': code.text.trim()});
        if (mounted) { flash(context, 'Kích hoạt thành công. Hãy đăng nhập.'); go('login'); }
      } else if (step == 'forgot') {
        final result = asMap(await api.post('/api/v1/mobile/account/forgot-password',
            {'identifier': identifier.text.trim()}));
        userId = str(result['userId']);
        if (mounted) { flash(context, str(result['message'])); go('verify'); }
      } else if (step == 'verify') {
        final result = asMap(await api.post('/api/v1/mobile/account/verify-reset',
            {'userId': userId, 'code': code.text.trim()}));
        resetToken = str(result['token']);
        if (mounted) go('reset');
      } else if (step == 'reset') {
        await api.post('/api/v1/mobile/account/reset-password',
            {'userId': userId, 'token': resetToken, 'password': password.text});
        session.value = asMap(await api.get('/api/v1/mobile/account/me'));
        if (mounted) Navigator.pop(context, true);
      }
    } on ApiException catch (ex) {
      if (mounted) flash(context, ex.message);
    } catch (_) {
      if (mounted) flash(context, 'Không kết nối được máy chủ CoCo.');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Widget input(TextEditingController controller, String label,
      {bool secret = false, TextInputType? keyboard, String? Function(String?)? validator}) =>
    Padding(padding: const EdgeInsets.only(bottom: 12), child: TextFormField(
      controller: controller, obscureText: secret, keyboardType: keyboard,
      decoration: InputDecoration(labelText: label),
      validator: validator ?? (v) => (v?.trim().isEmpty ?? true) ? 'Vui lòng nhập $label.' : null));

  @override
  Widget build(BuildContext context) {
    final title = switch (step) {
      'register' => 'Tạo tài khoản', 'resend' => 'Gửi lại mã kích hoạt',
      'activate' => 'Kích hoạt tài khoản', 'forgot' => 'Quên mật khẩu',
      'verify' => 'Nhập mã đặt lại', 'reset' => 'Mật khẩu mới', _ => 'Đăng nhập',
    };
    return Scaffold(appBar: AppBar(title: Text(title)), body: SafeArea(child: Center(
      child: SingleChildScrollView(padding: const EdgeInsets.all(24), child:
        ConstrainedBox(constraints: const BoxConstraints(maxWidth: 440), child:
          Form(key: form, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Center(child: Brand(height: 86)),
              const SizedBox(height: 18),
              Text(title, textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
              const SizedBox(height: 22),
              if (step == 'register') ...[
                input(name, 'Họ và tên'), input(email, 'Email', keyboard: TextInputType.emailAddress),
                input(phone, 'Số điện thoại', keyboard: TextInputType.phone,
                    validator: (v) => RegExp(r'^0[35789]\d{8}$').hasMatch(v ?? '')
                        ? null : 'Nhập số di động Việt Nam gồm 10 chữ số.'),
              ],
              if (['login', 'forgot', 'resend'].contains(step))
                input(identifier, 'Email hoặc số điện thoại'),
              if (['login', 'register', 'reset'].contains(step))
                input(password, 'Mật khẩu', secret: true,
                    validator: (v) => (v?.length ?? 0) >= 8 ? null : 'Mật khẩu cần ít nhất 8 ký tự.'),
              if (['register', 'reset'].contains(step))
                input(confirm, 'Xác nhận mật khẩu', secret: true,
                    validator: (v) => v == password.text ? null : 'Mật khẩu xác nhận chưa khớp.'),
              if (['activate', 'verify'].contains(step))
                input(code, 'Mã xác thực 6 số', keyboard: TextInputType.number,
                    validator: (v) => RegExp(r'^\d{6}$').hasMatch(v ?? '')
                        ? null : 'Nhập đúng 6 chữ số.'),
              const SizedBox(height: 12),
              FilledButton(onPressed: busy ? null : submit,
                  child: busy ? const CircularProgressIndicator(color: Colors.white)
                      : Text(title)),
              if (step == 'login') ...[
                TextButton(onPressed: () => go('register'), child: const Text('Chưa có tài khoản? Đăng ký')),
                TextButton(onPressed: () => go('forgot'), child: const Text('Quên mật khẩu')),
                TextButton(onPressed: () => go('resend'), child: const Text('Gửi lại mã kích hoạt')),
              ] else TextButton(onPressed: () => go('login'), child: const Text('Về đăng nhập')),
            ])))))));
  }
}

class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({super.key});
  @override
  State<ChangePasswordPage> createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final old = TextEditingController();
  final fresh = TextEditingController();
  @override
  void dispose() { old.dispose(); fresh.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Đổi mật khẩu')),
    body: ListView(padding: const EdgeInsets.all(20), children: [
      TextField(controller: old, obscureText: true,
          decoration: const InputDecoration(labelText: 'Mật khẩu hiện tại')),
      const SizedBox(height: 12),
      TextField(controller: fresh, obscureText: true,
          decoration: const InputDecoration(labelText: 'Mật khẩu mới')),
      const SizedBox(height: 20),
      FilledButton(onPressed: () => runAction(context, () async {
        await api.post('/api/v1/mobile/account/change-password',
            {'currentPassword': old.text, 'newPassword': fresh.text});
        session.value = asMap(await api.get('/api/v1/mobile/account/me'));
        if (context.mounted) Navigator.pop(context);
      }, success: 'Đã đổi mật khẩu.'), child: const Text('Lưu mật khẩu')),
    ]),
  );
}
