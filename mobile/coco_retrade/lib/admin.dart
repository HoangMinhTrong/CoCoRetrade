import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:qr_flutter/qr_flutter.dart';

import 'api.dart';
import 'social.dart';
import 'ui.dart';

class AdminPage extends StatelessWidget {
  const AdminPage({super.key, required this.permissions});
  final Set<String> permissions;
  @override
  Widget build(BuildContext context) {
    Widget entry(IconData icon, String label, Widget page) => Card(
      child: ListTile(leading: Icon(icon, color: rose), title: Text(label),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => openPage(context, page)));
    return Scaffold(appBar: AppBar(title: const Text('Quản trị CoCo')),
      body: ListView(padding: const EdgeInsets.all(14), children: [
        const SectionTitle('Công việc nhân viên'),
        if (permissions.contains('Products.ReadPending'))
          entry(Icons.fact_check_outlined, 'Ký gửi chờ kiểm định', const AdminProductsPage()),
        if (permissions.contains('Products.Manage'))
          entry(Icons.inventory_2_outlined, 'Quản lý sản phẩm',
              const AdminProductsPage(all: true)),
        if (permissions.contains('Orders.Manage'))
          entry(Icons.receipt_long_outlined, 'Quản lý đơn hàng', const AdminOrdersPage()),
        if (permissions.contains('Users.Manage')) ...[
          entry(Icons.people_outline, 'Quản lý người dùng', const AdminUsersPage()),
          entry(Icons.support_agent, 'Hộp hỗ trợ', const SupportPage(admin: true)),
        ],
        if (permissions.contains('Wallets.Manage')) ...[
          entry(Icons.account_balance_wallet_outlined, 'Ví & yêu cầu rút tiền',
              const AdminWalletPage()),
          entry(Icons.payments_outlined, 'Đối soát tiền người ký gửi',
              const AdminPayoutPage()),
          entry(Icons.query_stats, 'Doanh thu và hoa hồng', const RevenuePage()),
        ],
        if (permissions.contains('Audit.Read'))
          entry(Icons.history, 'Lịch sử thao tác', const AdminAuditPage()),
      ]));
  }
}

class AdminProductsPage extends StatefulWidget {
  const AdminProductsPage({super.key, this.all = false});
  final bool all;
  @override
  State<AdminProductsPage> createState() => _AdminProductsPageState();
}

class _AdminProductsPageState extends State<AdminProductsPage> {
  final search = TextEditingController();
  late Future<List<JsonMap>> rows = load();
  Future<List<JsonMap>> load() async => asList(await api.get(widget.all
      ? '/api/v1/admin/products' : '/api/v1/admin/inspections/pending',
      search.text.trim().isEmpty ? null : {'search': search.text.trim()}));
  void refresh() => setState(() => rows = load());
  @override
  void dispose() { search.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.all ? 'Quản lý sản phẩm' : 'Chờ kiểm định')),
    body: Column(children: [
      Padding(padding: const EdgeInsets.all(12), child: TextField(
        controller: search, onSubmitted: (_) => refresh(),
        decoration: InputDecoration(labelText: 'Mã ký gửi, SKU hoặc người bán',
            suffixIcon: IconButton(onPressed: refresh, icon: const Icon(Icons.search))))),
      Expanded(child: AsyncBody<List<JsonMap>>(future: rows, builder: (context, items) =>
        items.isEmpty ? const Center(child: Text('Không có sản phẩm phù hợp.'))
        : RefreshIndicator(onRefresh: () async { refresh(); await rows; },
          child: ListView.builder(itemCount: items.length, itemBuilder: (context, i) {
            final p = items[i];
            return Card(child: ListTile(title: Text(str(p['name'])),
              subtitle: Text('${str(p['consignmentCode'])} · ${str(p['sku'])}\n'
                  '${str(p['sellerName'])} · ${statusVi(p['status'])}'),
              isThreeLine: true, trailing: const Icon(Icons.chevron_right),
              onTap: () async {
                await Navigator.push(context, MaterialPageRoute<void>(builder: (_) =>
                    AdminInspectionPage(id: str(p['id']))));
                refresh();
              }));
          })))),
    ]),
  );
}

class AdminInspectionPage extends StatefulWidget {
  const AdminInspectionPage({super.key, required this.id});
  final String id;
  @override
  State<AdminInspectionPage> createState() => _AdminInspectionPageState();
}

class _AdminInspectionPageState extends State<AdminInspectionPage> {
  late Future<JsonMap> details = load();
  Future<JsonMap> load() async => asMap(await api.get('/api/v1/admin/inspections/${widget.id}'));
  void refresh() => setState(() => details = load());

  Future<void> inspect(JsonMap data) async {
    final p = asMap(data['product']);
    final name = TextEditingController(text: str(p['name']));
    final brand = TextEditingController(text: str(p['brand']));
    final category = TextEditingController(text: str(p['category']));
    final size = TextEditingController(text: str(p['size']));
    final original = TextEditingController(text: str(p['originalPrice']));
    final asking = TextEditingController(text: str(p['askingPrice']));
    final condition = TextEditingController(text: str(
        p['verifiedCondition'] ?? p['declaredCondition']));
    final notes = TextEditingController();
    List<XFile> images = [];
    bool passed = true;
    final saved = await showModalBottomSheet<bool>(context: context,
      isScrollControlled: true, builder: (sheet) => StatefulBuilder(
        builder: (sheet, update) => Padding(padding: EdgeInsets.fromLTRB(18, 18, 18,
          MediaQuery.viewInsetsOf(sheet).bottom + 18), child:
          SizedBox(height: MediaQuery.sizeOf(sheet).height * .75,
            child: ListView(children: [
              Text('Ghi kết quả kiểm định', style: Theme.of(sheet).textTheme.titleLarge),
              SwitchListTile(title: const Text('Đạt kiểm định'), value: passed,
                  onChanged: (v) => update(() => passed = v)),
              for (final item in <(String, TextEditingController)>[
                ('Tên sau kiểm định', name), ('Thương hiệu', brand),
                ('Danh mục', category), ('Kích cỡ', size),
                ('Giá gốc (VNĐ)', original), ('Giá niêm yết (VNĐ)', asking),
                ('Độ mới thực tế (%)', condition), ('Ghi chú (không bắt buộc)', notes)])
                Padding(padding: const EdgeInsets.only(bottom: 9), child: TextField(
                  controller: item.$2, decoration: InputDecoration(labelText: item.$1),
                  keyboardType: item.$1.contains('Giá') || item.$1.contains('(%)')
                      ? TextInputType.number : TextInputType.text)),
              OutlinedButton.icon(onPressed: () async {
                final picked = await ImagePicker().pickMultiImage();
                update(() => images = [...images, ...picked]);
              }, icon: const Icon(Icons.add_photo_alternate_outlined),
                  label: Text('Ảnh kiểm định bổ sung (${images.length})')),
              OutlinedButton.icon(onPressed: () async {
                final photo = await ImagePicker().pickImage(
                    source: ImageSource.camera, imageQuality: 90);
                if (photo != null) update(() => images = [...images, photo]);
              }, icon: const Icon(Icons.camera_alt_outlined),
                  label: const Text('Chụp ảnh kiểm định')),
              FilledButton(onPressed: () => Navigator.pop(sheet, true),
                  child: const Text('Lưu kết quả kiểm định')),
            ])))));
    if (saved == true && mounted) {
      await runAction(context, () async {
        await api.multipart('/api/v1/admin/inspections/${widget.id}', {
          'ProductId': widget.id, 'ExpectedRowVersion': str(data['rowVersion']),
          'Passed': '$passed', 'Name': name.text.trim(), 'Brand': brand.text.trim(),
          'Category': category.text.trim(), 'Size': size.text.trim(),
          'OriginalPrice': original.text, 'AskingPrice': asking.text,
          'VerifiedCondition': condition.text, 'Notes': notes.text,
        }, images, 'InspectionImages');
        refresh();
      }, success: 'Đã lưu kết quả kiểm định.');
    }
    for (final c in [name, brand, category, size, original, asking, condition, notes]) { c.dispose(); }
  }

  Future<void> publish(JsonMap data) async {
    bool checked = false;
    final confirmed = await showDialog<bool>(context: context,
      builder: (dialog) => StatefulBuilder(builder: (dialog, update) => AlertDialog(
        title: const Text('Duyệt và đăng bán'),
        content: CheckboxListTile(value: checked,
          title: const Text('Tôi đã kiểm tra sản phẩm thực tế tại CoCo.'),
          onChanged: (v) => update(() => checked = v ?? false)),
        actions: [TextButton(onPressed: () => Navigator.pop(dialog, false),
            child: const Text('Đóng')),
          FilledButton(onPressed: checked ? () => Navigator.pop(dialog, true) : null,
              child: const Text('Đăng bán'))])));
    if (confirmed != true || !mounted) return;
    await runAction(context, () async {
      await api.post('/api/v1/admin/inspections/${widget.id}/publish', {
        'expectedRowVersion': data['rowVersion'], 'physicalItemConfirmed': true,
      });
      refresh();
    }, success: 'Sản phẩm đã được đăng bán.');
  }

  Future<void> reject(JsonMap data) async {
    final reason = await showDialog<String>(context: context,
      builder: (dialog) {
        final field = TextEditingController();
        return AlertDialog(title: const Text('Lý do từ chối'),
          content: TextField(controller: field,
              decoration: const InputDecoration(labelText: 'Lý do cụ thể')),
          actions: [TextButton(onPressed: () => Navigator.pop(dialog), child: const Text('Đóng')),
            FilledButton(onPressed: () => Navigator.pop(dialog, field.text.trim()),
                child: const Text('Từ chối'))]);
      });
    if (reason == null || reason.isEmpty || !mounted) return;
    await runAction(context, () async {
      await api.post('/api/v1/admin/inspections/${widget.id}/reject', {
        'expectedRowVersion': data['rowVersion'], 'reason': reason,
      });
      refresh();
    }, success: 'Đã thông báo lý do từ chối cho người bán.');
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Kiểm định sản phẩm')),
    body: AsyncBody<JsonMap>(future: details, builder: (context, data) {
      final p = asMap(data['product']);
      final images = p['imageIds'] is List ? p['imageIds'] as List : [];
      final perms = (session.value?['permissions'] is List
          ? session.value!['permissions'] as List : []);
      return ListView(padding: const EdgeInsets.all(16), children: [
        if (images.isNotEmpty) SizedBox(height: 250, child: PageView(
            children: images.map((id) => productImage(id)).toList())),
        Card(child: Padding(padding: const EdgeInsets.all(17), child: Column(children: [
          Text(str(p['name']), style: Theme.of(context).textTheme.titleLarge),
          labeled('Mã ký gửi', str(p['consignmentCode'])),
          labeled('SKU', str(p['sku']).isEmpty ? 'Chưa cấp' : str(p['sku'])),
          labeled('Người bán', str(data['sellerName'])),
          labeled('Tình trạng', statusVi(p['status'])),
          labeled('Giá', money(p['askingPrice'])),
          labeled('Độ mới', '${p['verifiedCondition'] ?? p['declaredCondition']}%'),
          labeled('Phiên bản dữ liệu', str(data['rowVersion'])),
        ]))),
        if (perms.contains('Products.Inspect') && str(p['status']) == 'PendingApproval')
          FilledButton(onPressed: () => inspect(data), child: const Text('Ghi kết quả kiểm định')),
        if (perms.contains('Products.Publish') && data['canPublish'] == true)
          FilledButton(onPressed: () => publish(data), child: const Text('Duyệt & đăng bán')),
        if (perms.contains('Products.Reject') && str(p['status']) == 'PendingApproval')
          OutlinedButton(onPressed: () => reject(data), child: const Text('Từ chối')),
        if (str(p['sku']).isNotEmpty) OutlinedButton.icon(
          icon: const Icon(Icons.qr_code), label: const Text('Xem tem QR / SKU'),
          onPressed: () => showDialog<void>(context: context,
            builder: (dialog) => AlertDialog(title: const Text('Tem sản phẩm CoCo'),
              content: Column(mainAxisSize: MainAxisSize.min, children: [
                QrImageView(data: str(p['sku']), size: 190),
                SelectableText(str(p['sku']), style: const TextStyle(
                    fontSize: 20, fontWeight: FontWeight.w800)),
              ]),
              actions: [TextButton(onPressed: () => Navigator.pop(dialog),
                  child: const Text('Đóng'))]))),
        const SectionTitle('Lịch sử kiểm định'),
        for (final inspection in asList(data['inspections'])) Card(child: ListTile(
          title: Text(inspection['passed'] == true ? 'Đạt kiểm định' : 'Không đạt'),
          subtitle: Text(str(inspection['notes']).isEmpty ?
              localDate(inspection['createdAtUtc'])
              : '${localDate(inspection['createdAtUtc'])}\n${inspection['notes']}'))),
      ]);
    }),
  );
}

class AdminOrdersPage extends StatelessWidget {
  const AdminOrdersPage({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Quản lý đơn hàng')),
    body: AsyncBody<List<JsonMap>>(future: api.get('/api/v1/admin/orders').then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Chưa có đơn hàng.'))
        : ListView(children: rows.map((o) => Card(child: ListTile(
          title: Text(str(o['productName'])),
          subtitle: Text('${money(o['totalAmount'])} · ${statusVi(o['status'])}'),
          onTap: () => openPage(context, OrderPage(id: str(o['id']), admin: true))))).toList())),
  );
}

class AdminUsersPage extends StatefulWidget {
  const AdminUsersPage({super.key});
  @override
  State<AdminUsersPage> createState() => _AdminUsersPageState();
}

class _AdminUsersPageState extends State<AdminUsersPage> {
  late Future<List<JsonMap>> users = load();
  Future<List<JsonMap>> load() async => asList(await api.get('/api/v1/admin/users'));
  void refresh() => setState(() => users = load());
  Future<void> change(JsonMap u, String action, bool enabled) async {
    await runAction(context, () async {
      await api.post('/api/v1/admin/users/${u['id']}/$action', {'enabled': enabled});
      refresh();
    }, success: 'Đã cập nhật tài khoản.');
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Quản lý người dùng')),
    body: AsyncBody<List<JsonMap>>(future: users, builder: (context, rows) =>
      ListView(children: rows.map((u) => Card(child: ExpansionTile(
        title: Text(str(u['fullName'])), subtitle: Text(str(u['email'])),
        children: [
          SwitchListTile(title: const Text('Khóa tài khoản'),
            value: u['isLocked'] == true,
            onChanged: (v) => change(u, 'lock', v)),
          SwitchListTile(title: const Text('Quyền kiểm định'),
            value: u['isInspector'] == true,
            onChanged: (v) => change(u, 'inspector', v)),
          SwitchListTile(title: const Text('Quyền đăng bán sản phẩm'),
            value: u['hasDirectPublishGrant'] == true,
            onChanged: (v) => change(u, 'publish-grant', v)),
        ]))).toList())),
  );
}

class AdminWalletPage extends StatefulWidget {
  const AdminWalletPage({super.key});
  @override
  State<AdminWalletPage> createState() => _AdminWalletPageState();
}

class AdminPayoutPage extends StatefulWidget {
  const AdminPayoutPage({super.key});
  @override
  State<AdminPayoutPage> createState() => _AdminPayoutPageState();
}

class _AdminPayoutPageState extends State<AdminPayoutPage> {
  late Future<List<JsonMap>> rows = load();
  Future<List<JsonMap>> load() async =>
      asList(await api.get('/api/v1/admin/wallets/payout-candidates'));
  void refresh() => setState(() => rows = load());
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Đối soát người ký gửi')),
    body: AsyncBody<List<JsonMap>>(future: rows, builder: (context, items) =>
      items.isEmpty ? const Center(child: Text('Không có đơn chờ ghi ví.'))
      : ListView(children: items.map((p) => Card(child: Padding(
        padding: const EdgeInsets.all(14), child: Column(children: [
          Text('${str(p['productSku'])} · ${str(p['sellerName'])}',
              style: const TextStyle(fontWeight: FontWeight.w800)),
          labeled('Tiền hàng', money(p['merchandiseAmount'])),
          labeled('Hoa hồng CoCo', money(p['commissionAmount'])),
          labeled('Người ký gửi nhận', money(p['netAmount']), valueColor: rose),
          FilledButton(onPressed: () async {
            final confirmed = await showDialog<bool>(context: context,
              builder: (dialog) => AlertDialog(
                title: const Text('Ghi tiền vào ví người ký gửi?'),
                content: const Text('Chỉ xác nhận sau khi đơn đã hoàn tất '
                    'và khoản thu đã được đối soát.'),
                actions: [TextButton(onPressed: () => Navigator.pop(dialog, false),
                    child: const Text('Đóng')),
                  FilledButton(onPressed: () => Navigator.pop(dialog, true),
                      child: const Text('Xác nhận'))]));
            if (confirmed != true || !context.mounted) return;
            await runAction(context, () async {
              await api.post('/api/v1/admin/wallets/payouts/${p['orderId']}',
                  {'confirmed': true});
              refresh();
            }, success: 'Đã ghi tiền vào ví người ký gửi.');
          }, child: const Text('Xác nhận ghi ví')),
        ])))).toList())),
  );
}

class _AdminWalletPageState extends State<AdminWalletPage> {
  late Future<List<JsonMap>> withdrawals = load();
  Future<List<JsonMap>> load() async =>
      asList(await api.get('/api/v1/admin/wallets/withdrawals'));
  void refresh() => setState(() => withdrawals = load());
  Future<void> command(JsonMap w, String action, [JsonMap? body]) async {
    await runAction(context, () async {
      await api.post('/api/v1/admin/wallets/withdrawals/${w['id']}/$action', body);
      refresh();
    }, success: 'Đã cập nhật yêu cầu rút tiền.');
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Rút tiền ký gửi')),
    body: AsyncBody<List<JsonMap>>(future: withdrawals, builder: (context, rows) =>
      rows.isEmpty ? const Center(child: Text('Chưa có yêu cầu rút tiền.'))
      : ListView(children: rows.map((w) => Card(child: ExpansionTile(
        title: Text('${str(w['ownerName'])} · ${money(w['amount'])}'),
        subtitle: Text('${statusVi(w['status'])} · ${str(w['bankName'])} '
            '${str(w['bankAccountNumber'])}'),
        children: [
          if (str(w['status']) == 'Pending') ...[
            TextButton(onPressed: () => command(w, 'approve'),
                child: const Text('Duyệt yêu cầu')),
            TextButton(onPressed: () async {
              final reason = await _askText(context, 'Lý do từ chối');
              if (reason != null) await command(w, 'reject', {'reason': reason});
            }, child: const Text('Từ chối')),
          ],
          if (str(w['status']) == 'Approved') TextButton(
            onPressed: () async {
              final reference = await _askText(context,
                  'Mã giao dịch sau khi bạn đã chuyển khoản thực tế');
              if (reference != null) await command(w, 'confirm-transfer',
                  {'transferReference': reference, 'confirmed': true});
            }, child: const Text('Xác nhận đã chuyển khoản')),
        ]))).toList())),
  );
}

Future<String?> _askText(BuildContext context, String title) async {
  final field = TextEditingController();
  final value = await showDialog<String>(context: context,
    builder: (dialog) => AlertDialog(title: Text(title),
      content: TextField(controller: field),
      actions: [TextButton(onPressed: () => Navigator.pop(dialog), child: const Text('Đóng')),
        FilledButton(onPressed: () => Navigator.pop(dialog, field.text.trim()),
            child: const Text('Xác nhận'))]));
  field.dispose();
  return value?.isEmpty == true ? null : value;
}

class RevenuePage extends StatefulWidget {
  const RevenuePage({super.key});
  @override
  State<RevenuePage> createState() => _RevenuePageState();
}

class _RevenuePageState extends State<RevenuePage> {
  String period = 'month';
  late Future<JsonMap> report = load();
  Future<JsonMap> load() async => asMap(await api.get(
      '/api/v1/admin/reports/revenue', {'period': period}));
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Doanh thu CoCo')),
    body: ListView(padding: const EdgeInsets.all(18), children: [
      SegmentedButton<String>(segments: const [
        ButtonSegment(value: 'day', label: Text('Ngày')),
        ButtonSegment(value: 'week', label: Text('Tuần')),
        ButtonSegment(value: 'month', label: Text('Tháng'))],
        selected: {period}, onSelectionChanged: (v) => setState(() {
          period = v.first; report = load();
        })),
      const SizedBox(height: 16),
      AsyncBody<JsonMap>(future: report, builder: (context, r) =>
        Card(child: Padding(padding: const EdgeInsets.all(18), child: Column(children: [
          labeled('Từ ngày', str(r['fromDate'])),
          labeled('Đến ngày', str(r['throughDate'])),
          labeled('Đơn hoàn tất', str(r['completedOrderCount'])),
          labeled('Hoa hồng CoCo', money(r['commissionAmount']), valueColor: rose),
          labeled('Tiền người ký gửi', money(r['sellerNetAmount'])),
          labeled('Đã ghi ví người ký gửi', money(r['sellerWalletCreditedAmount'])),
          labeled('Chờ ghi ví', money(r['sellerWalletPendingAmount'])),
        ])))),
    ]),
  );
}

class AdminAuditPage extends StatelessWidget {
  const AdminAuditPage({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Lịch sử thao tác')),
    body: AsyncBody<List<JsonMap>>(future: api.get('/api/v1/admin/products/audit').then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Chưa có lịch sử thao tác.'))
        : ListView(children: rows.map((a) => Card(child: ListTile(
          title: Text(str(a['action'])), subtitle: Text(str(a['details'])),
          trailing: Text(localDate(a['createdAtUtc']))))).toList())),
  );
}
