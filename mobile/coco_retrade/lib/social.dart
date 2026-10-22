import 'dart:async';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import 'admin.dart';
import 'api.dart';
import 'auth.dart';
import 'market.dart';
import 'realtime.dart';
import 'ui.dart';

class NotificationsPage extends StatefulWidget {
  const NotificationsPage({super.key});
  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  String group = 'transactions';
  Future<List<JsonMap>>? rows;
  void load() => setState(() => rows = api.get('/api/v1/notifications',
      {'group': group}).then(asList));
  @override
  void initState() { super.initState(); if (session.value != null) load(); }
  @override
  Widget build(BuildContext context) {
    if (session.value == null) return Center(child: TextButton(
        onPressed: () async { if (await needAccount(context)) load(); },
        child: const Text('Đăng nhập để xem thông báo')));
    rows ??= api.get('/api/v1/notifications', {'group': group}).then(asList);
    return Column(children: [
      SizedBox(height: 54, child: ListView(scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 14), children: [
          for (final item in const [
            ('transactions', 'Giao dịch'), ('listings', 'Sản phẩm'), ('social', 'Tương tác')])
            Padding(padding: const EdgeInsets.only(right: 7), child: ChoiceChip(
              label: Text(item.$2), selected: group == item.$1,
              onSelected: (_) { group = item.$1; load(); })),
        ])),
      Expanded(child: RefreshIndicator(onRefresh: () async { load(); await rows; },
        child: AsyncBody<List<JsonMap>>(future: rows!, builder: (context, items) =>
          items.isEmpty ? const Center(child: Text('Chưa có thông báo.'))
          : ListView.builder(itemCount: items.length, itemBuilder: (context, i) {
            final n = items[i];
            return Card(margin: const EdgeInsets.fromLTRB(12, 4, 12, 4),
              child: ListTile(
                leading: CircleAvatar(backgroundColor: n['isRead'] == true ?
                    const Color(0xfff1f1f1) : softRose,
                    child: Icon(n['isRead'] == true ? Icons.notifications_none :
                        Icons.notifications_active_outlined, color: rose)),
                title: Text(str(n['message']), maxLines: 3, overflow: TextOverflow.ellipsis),
                subtitle: Text(localDate(n['createdAtUtc'])),
                trailing: const Icon(Icons.chevron_right), onTap: () async {
                  await runAction(context, () async {
                    await api.post('/api/v1/notifications/${n['id']}/read');
                    if (!context.mounted) return;
                    if (n['conversationId'] != null) openPage(context,
                        ConversationPage(id: str(n['conversationId'])));
                    else if (n['offerId'] != null) openPage(context,
                        OfferPage(id: str(n['offerId'])));
                    else if (n['orderId'] != null) openPage(context,
                        OrderPage(id: str(n['orderId'])));
                    else if (n['productId'] != null) {
                      final permissions = session.value?['permissions'] is List
                          ? session.value!['permissions'] as List : [];
                      if (str(n['category']) == 'Listings' &&
                          permissions.contains('Products.ReadPending')) {
                        openPage(context, AdminInspectionPage(id: str(n['productId'])));
                      } else {
                        openPage(context, ProductPage(id: str(n['productId']),
                            ownListing: str(n['category']) == 'Listings'));
                      }
                    }
                    else if (str(n['category']).toLowerCase().contains('cart'))
                      openPage(context, const ProductListPage(title: 'Giỏ hàng',
                          path: '/api/v1/cart'));
                    else if (str(n['category']).toLowerCase() == 'support')
                      openPage(context, const SupportPage());
                    load();
                  });
                },
              ));
          })))),
    ]);
  }
}

class ProductListPage extends StatefulWidget {
  const ProductListPage({super.key, required this.title, required this.path});
  final String title; final String path;
  @override
  State<ProductListPage> createState() => _ProductListPageState();
}

class _ProductListPageState extends State<ProductListPage> {
  late Future<List<JsonMap>> rows = load();
  Future<List<JsonMap>> load() async => asList(await api.get(widget.path));
  void refresh() => setState(() => rows = load());
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.title)),
    body: AsyncBody<List<JsonMap>>(future: rows, builder: (context, items) =>
      items.isEmpty ? const Center(child: Text('Chưa có sản phẩm.'))
      : ListView.builder(itemCount: items.length, itemBuilder: (context, i) {
        final p = items[i];
        final isActive = str(p['status']) == 'Active';
        return Card(margin: const EdgeInsets.fromLTRB(12, 5, 12, 5),
          child: ListTile(leading: SizedBox(width: 60, height: 60,
              child: productImage(p['coverImageId'])),
            title: Text(str(p['name'])),
            subtitle: Text('${money(p['askingPrice'])} · '
                '${isActive ? 'Đang bán' : statusVi(p['status'])}\n'
                '${str(p['sku']).isNotEmpty ? p['sku'] : p['consignmentCode']}'),
            isThreeLine: true,
            trailing: widget.path == '/api/v1/cart' ||
                widget.path == '/api/v1/social/favorites' ? IconButton(
              icon: const Icon(Icons.delete_outline), tooltip: 'Bỏ khỏi danh sách',
              onPressed: () => runAction(context, () async {
                await api.delete(widget.path == '/api/v1/cart'
                    ? '/api/v1/cart/items/${p['id']}'
                    : '/api/v1/social/favorites/${p['id']}');
                refresh();
              })) : null,
            onTap: isActive || widget.path == '/api/v1/listings/mine'
                ? () => openPage(context, ProductPage(id: str(p['id']),
                    ownListing: widget.path == '/api/v1/listings/mine'))
                : null));
      })),
  );
}

class OfferListPage extends StatelessWidget {
  const OfferListPage({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Đề xuất giá')),
    body: AsyncBody<List<JsonMap>>(
      future: api.get('/api/v1/offers').then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Chưa có đề xuất giá.'))
        : ListView.builder(itemCount: rows.length, itemBuilder: (context, i) {
            final o = rows[i];
            return Card(child: ListTile(title: Text(str(o['productName'])),
              subtitle: Text('${money(o['currentPrice'])} · ${statusVi(o['status'])}'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => openPage(context, OfferPage(id: str(o['id'])))));
          }),
    ),
  );
}

class OrdersPage extends StatelessWidget {
  const OrdersPage({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Đơn hàng của tôi')),
    body: AsyncBody<List<JsonMap>>(future: api.get('/api/v1/orders').then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Bạn chưa có đơn hàng.'))
        : ListView.builder(itemCount: rows.length, itemBuilder: (context, i) {
          final o = rows[i];
          return Card(child: ListTile(title: Text(str(o['productName'])),
          subtitle: Text('${money(o['totalAmount'])} · ${statusVi(o['status'])}'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => openPage(context, OrderPage(id: str(o['id'])))));
        })),
  );
}

class OrderPage extends StatefulWidget {
  const OrderPage({super.key, required this.id, this.admin = false});
  final String id; final bool admin;
  @override
  State<OrderPage> createState() => _OrderPageState();
}

class _OrderPageState extends State<OrderPage> {
  late Future<JsonMap> order = load();
  Future<JsonMap> load() async => asMap(await api.get(
      '${widget.admin ? '/api/v1/admin' : '/api/v1'}/orders/${widget.id}'));
  void refresh() => setState(() => order = load());
  Future<void> command(String path, [JsonMap? body]) async => runAction(context, () async {
    await api.post('${widget.admin ? '/api/v1/admin' : '/api/v1'}'
        '/orders/${widget.id}/$path', body);
    refresh();
  }, success: 'Đơn hàng đã được cập nhật.');
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Chi tiết đơn hàng')),
    body: AsyncBody<JsonMap>(future: order, builder: (context, o) =>
      ListView(padding: const EdgeInsets.all(18), children: [
        Card(child: Padding(padding: const EdgeInsets.all(17),
          child: Column(children: [
            Text(str(o['productName']), style: Theme.of(context).textTheme.titleLarge),
            labeled('Mã sản phẩm', str(o['productSku'])),
            labeled('Trạng thái', statusVi(o['status'])),
            labeled('Thanh toán', statusVi(o['paymentStatus'])),
            labeled('Hình thức', str(o['fulfillment']) == 'Pickup'
                ? 'Đến CoCo lấy' : 'Giao tận nơi'),
            labeled('Người nhận', str(o['recipientName'])),
            labeled('Điện thoại', str(o['recipientPhone'])),
            if (o['pickupAtUtc'] != null)
              labeled('Giờ hẹn lấy', localDate(o['pickupAtUtc'])),
            if (o['pickupAddress'] != null)
              labeled('Địa chỉ CoCo', str(o['pickupAddress'])),
            if (o['pickupDeadlineAtUtc'] != null)
              labeled('Hạn lấy theo giá thỏa thuận',
                  localDate(o['pickupDeadlineAtUtc'])),
            if (o['streetAddress'] != null)
              labeled('Địa chỉ', '${o['streetAddress']}, ${o['ward']}, ${o['province']}'),
            const Divider(),
            labeled('Tiền hàng', money(o['merchandisePrice'])),
            labeled('Phí giao', money(o['shippingFee'])),
            labeled('Tổng cộng', money(o['totalAmount']), valueColor: rose),
            if (widget.admin)
              labeled('Hoa hồng CoCo', money(o['commissionAmount'])),
          ]))),
        const SizedBox(height: 14),
        if (!widget.admin && str(o['status']) == 'PendingProcessing')
          OutlinedButton(onPressed: () => command('cancel'),
              child: const Text('Yêu cầu hủy đơn')),
        if (widget.admin && str(o['status']) == 'PendingProcessing')
          FilledButton(onPressed: () => command('ready'),
              child: const Text('Sẵn sàng giao / lấy')),
        if (widget.admin && str(o['paymentStatus']) == 'Unpaid'
            && ['Shipping', 'ReadyForPickup'].contains(str(o['status'])))
          OutlinedButton(onPressed: () async {
            final reference = await _ask(context, 'Mã xác nhận đã thu tiền');
            if (reference != null) await command('confirm-payment', {'reference': reference});
          }, child: const Text('Xác nhận đã thu tiền mặt')),
        if (widget.admin && str(o['paymentStatus']) == 'Paid'
            && ['Shipping', 'ReadyForPickup'].contains(str(o['status'])))
          OutlinedButton(onPressed: () => command('complete'),
              child: const Text('Xác nhận giao thành công')),
        if (widget.admin && str(o['paymentStatus']) == 'Unpaid'
            && ['PendingProcessing', 'ReadyForPickup'].contains(str(o['status'])))
          TextButton(onPressed: () => command('cancel'),
              child: const Text('Hủy đơn và mở bán lại')),
        if (widget.admin && str(o['paymentStatus']) == 'Unpaid'
            && str(o['status']) == 'Shipping')
          TextButton(onPressed: () async {
            final confirmed = await showDialog<bool>(context: context,
              builder: (dialog) => AlertDialog(
                title: const Text('Hàng không được nhận'),
                content: const Text('Chỉ mở bán lại sau khi món đồ đã về CoCo.'),
                actions: [TextButton(onPressed: () => Navigator.pop(dialog, false),
                    child: const Text('Đóng')),
                  FilledButton(onPressed: () => Navigator.pop(dialog, true),
                    child: const Text('Tôi xác nhận hàng đã về'))]));
            if (confirmed == true) {
              await command('return-undelivered', {'itemReturnedToStore': true});
            }
          }, child: const Text('Khách không nhận · hàng đã về CoCo')),
      ])),
  );
}

Future<String?> _ask(BuildContext context, String title) async {
  final text = TextEditingController();
  final answer = await showDialog<String>(context: context, builder: (dialog) =>
      AlertDialog(title: Text(title),
        content: TextField(controller: text, decoration: const InputDecoration(labelText: 'Nội dung')),
        actions: [TextButton(onPressed: () => Navigator.pop(dialog), child: const Text('Đóng')),
          FilledButton(onPressed: () => Navigator.pop(dialog, text.text.trim()),
              child: const Text('Xác nhận'))]));
  text.dispose();
  return answer?.isEmpty == true ? null : answer;
}

class ConversationListPage extends StatelessWidget {
  const ConversationListPage({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Tin nhắn')),
    body: AsyncBody<List<JsonMap>>(future: api.get('/api/v1/messages').then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Chưa có cuộc trò chuyện.'))
        : ListView.builder(itemCount: rows.length, itemBuilder: (context, i) {
          final m = rows[i];
          return Card(child: ListTile(title: Text(str(m['otherUserName'])),
            subtitle: Text(str(m['productName'])),
            trailing: number(m['unreadCount']) > 0 ? CircleAvatar(radius: 13,
                backgroundColor: rose, child: Text(str(m['unreadCount']),
                    style: const TextStyle(color: Colors.white))) : null,
            onTap: () => openPage(context, ConversationPage(id: str(m['id'])))));
        })),
  );
}

class ConversationPage extends StatefulWidget {
  const ConversationPage({super.key, required this.id});
  final String id;
  @override
  State<ConversationPage> createState() => _ConversationPageState();
}

class _ConversationPageState extends State<ConversationPage> {
  final input = TextEditingController();
  Timer? poll;
  CoCoRealtime? live;
  Future<JsonMap>? details;
  void refresh() => setState(() => details = api.get('/api/v1/messages/${widget.id}').then(asMap));
  @override
  void initState() {
    super.initState(); refresh();
    live = CoCoRealtime(api, '/hubs/conversations', 'JoinConversation', widget.id,
        'MessageReceived', () { if (mounted) refresh(); })..connect();
    poll = Timer.periodic(const Duration(seconds: 30), (_) { if (mounted) refresh(); });
  }
  @override
  void dispose() { poll?.cancel(); live?.dispose(); input.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Nhắn người bán')),
    body: Column(children: [
      Expanded(child: AsyncBody<JsonMap>(future: details!, builder: (context, data) {
        final items = asList(data['messages']);
        return ListView.builder(reverse: true, padding: const EdgeInsets.all(14),
          itemCount: items.length, itemBuilder: (context, i) {
            final m = items[items.length - 1 - i];
            final mine = str(m['senderId']) == str(session.value?['id']);
            return Align(alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
              child: Container(margin: const EdgeInsets.symmetric(vertical: 4),
                constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * .78),
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: mine ? softRose : Colors.white,
                    borderRadius: BorderRadius.circular(16)),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(str(m['body'])),
                  const SizedBox(height: 4),
                  Text(localDate(m['createdAtUtc']),
                      style: const TextStyle(fontSize: 11, color: Colors.black45)),
                ])));
          });
      })),
      SafeArea(child: Padding(padding: const EdgeInsets.all(10), child: Row(children: [
        Expanded(child: TextField(controller: input,
            decoration: const InputDecoration(hintText: 'Nhập tin nhắn...'))),
        IconButton(icon: const Icon(Icons.send, color: rose), onPressed: () async {
          final text = input.text.trim(); if (text.isEmpty) return;
          await runAction(context, () async {
            await api.post('/api/v1/messages/${widget.id}/messages', {'body': text});
            input.clear(); refresh();
          });
        }),
      ]))),
    ]),
  );
}

class SupportPage extends StatefulWidget {
  const SupportPage({super.key, this.admin = false});
  final bool admin;
  @override
  State<SupportPage> createState() => _SupportPageState();
}

class _SupportPageState extends State<SupportPage> {
  late Future<List<JsonMap>> tickets = load();
  Future<List<JsonMap>> load() async => asList(await api.get(widget.admin
      ? '/api/v1/admin/support' : '/api/v1/support'));
  void refresh() => setState(() => tickets = load());
  Future<void> create() async {
    final subject = await _ask(context, 'Chủ đề cần hỗ trợ');
    if (subject == null || !mounted) return;
    final body = await _ask(context, 'Nội dung yêu cầu');
    if (body == null || !mounted) return;
    await runAction(context, () async {
      final result = asMap(await api.post('/api/v1/support',
          {'subject': subject, 'body': body}));
      refresh();
      if (mounted) openPage(context, SupportDetailPage(id: str(asMap(result['ticket'])['id'])));
    });
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.admin ? 'Hộp hỗ trợ' : 'Hỗ trợ CoCo')),
    floatingActionButton: widget.admin ? null : FloatingActionButton.extended(
        onPressed: create, icon: const Icon(Icons.add), label: const Text('Gửi yêu cầu')),
    body: AsyncBody<List<JsonMap>>(future: tickets, builder: (context, rows) =>
      rows.isEmpty ? const Center(child: Text('Chưa có yêu cầu hỗ trợ.'))
      : ListView.builder(itemCount: rows.length, itemBuilder: (context, i) {
        final t = rows[i];
        return Card(child: ListTile(title: Text(str(t['subject'])),
          subtitle: Text('${statusVi(t['status'])} · ${localDate(t['lastActivityAtUtc'])}'),
          onTap: () => openPage(context,
              SupportDetailPage(id: str(t['id']), admin: widget.admin))));
      })),
  );
}

class SupportDetailPage extends StatefulWidget {
  const SupportDetailPage({super.key, required this.id, this.admin = false});
  final String id; final bool admin;
  @override
  State<SupportDetailPage> createState() => _SupportDetailPageState();
}

class _SupportDetailPageState extends State<SupportDetailPage> {
  final input = TextEditingController(); Timer? poll; CoCoRealtime? live;
  late Future<JsonMap> details = load();
  Future<JsonMap> load() async => asMap(await api.get(
      '${widget.admin ? '/api/v1/admin' : '/api/v1'}/support/${widget.id}'));
  void refresh() => setState(() => details = load());
  @override
  void initState() {
    super.initState();
    live = CoCoRealtime(api, '/hubs/support', 'JoinTicket', widget.id,
        'SupportTicketChanged', () { if (mounted) refresh(); })..connect();
    poll = Timer.periodic(const Duration(seconds: 30), (_) { if (mounted) refresh(); });
  }
  @override
  void dispose() { poll?.cancel(); live?.dispose(); input.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Trao đổi hỗ trợ')),
    body: Column(children: [
      Expanded(child: AsyncBody<JsonMap>(future: details, builder: (context, data) =>
        ListView(padding: const EdgeInsets.all(16), children: [
          Text(str(asMap(data['ticket'])['subject']),
              style: Theme.of(context).textTheme.titleLarge),
          for (final m in asList(data['messages'])) Card(child: Padding(
            padding: const EdgeInsets.all(12), child: Column(
              crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(str(m['body'])),
                Text(localDate(m['createdAtUtc']),
                    style: const TextStyle(color: Colors.black45, fontSize: 11)),
              ]))),
          if (widget.admin && str(asMap(data['ticket'])['status']) != 'Closed')
            TextButton(onPressed: () => runAction(context, () async {
              await api.post('/api/v1/admin/support/${widget.id}/close'); refresh();
            }), child: const Text('Đóng yêu cầu')),
        ]))),
      SafeArea(child: Padding(padding: const EdgeInsets.all(10), child: Row(children: [
        Expanded(child: TextField(controller: input,
            decoration: const InputDecoration(hintText: 'Nhập câu trả lời...'))),
        IconButton(icon: const Icon(Icons.send, color: rose), onPressed: () {
          final body = input.text.trim(); if (body.isEmpty) return;
          runAction(context, () async {
            await api.post('${widget.admin ? '/api/v1/admin' : '/api/v1'}'
                '/support/${widget.id}/replies', {'body': body});
            input.clear(); refresh();
          });
        }),
      ]))),
    ]),
  );
}

class WalletPage extends StatefulWidget {
  const WalletPage({super.key});
  @override
  State<WalletPage> createState() => _WalletPageState();
}

class _WalletPageState extends State<WalletPage> {
  late Future<JsonMap> balance = api.get('/api/v1/wallet').then(asMap);
  late Future<List<JsonMap>> withdrawals = api.get('/api/v1/wallet/withdrawals').then(asList);
  void refresh() => setState(() {
    balance = api.get('/api/v1/wallet').then(asMap);
    withdrawals = api.get('/api/v1/wallet/withdrawals').then(asList);
  });
  Future<void> requestWithdrawal() async {
    final amount = await _ask(context, 'Số tiền muốn rút (VNĐ)');
    if (amount == null || !mounted) return;
    final bank = await _ask(context, 'Tên ngân hàng');
    if (bank == null || !mounted) return;
    final account = await _ask(context, 'Số tài khoản');
    if (account == null || !mounted) return;
    final parsedAmount = num.tryParse(amount);
    if (parsedAmount == null || parsedAmount <= 0) {
      flash(context, 'Số tiền muốn rút không hợp lệ.'); return;
    }
    await runAction(context, () async {
      await api.post('/api/v1/wallet/withdrawals', {
        'requestKey': newKey(), 'amount': parsedAmount,
        'bankName': bank, 'bankAccountNumber': account,
      });
      refresh();
    }, success: 'Đã gửi yêu cầu rút tiền.');
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Ví ký gửi CoCo')),
    body: ListView(padding: const EdgeInsets.all(16), children: [
      AsyncBody<JsonMap>(future: balance, builder: (context, b) =>
        Card(child: Padding(padding: const EdgeInsets.all(18), child: Column(children: [
          labeled('Khả dụng', money(b['available']), valueColor: rose),
          labeled('Đang chờ đối soát', money(b['pendingSellerProceeds'])),
          labeled('Đang chờ rút', money(b['pendingWithdrawal'])),
          labeled('Đã rút', money(b['withdrawn'])),
        ])))),
      const SizedBox(height: 12),
      FilledButton(onPressed: requestWithdrawal, child: const Text('Yêu cầu rút tiền')),
      const SectionTitle('Lịch sử yêu cầu'),
      AsyncBody<List<JsonMap>>(future: withdrawals, builder: (context, rows) =>
        rows.isEmpty ? const Text('Chưa có yêu cầu rút tiền.')
        : Column(children: rows.map((w) => Card(child: ListTile(
            title: Text(money(w['amount'])),
            subtitle: Text('${str(w['bankName'])} · ${str(w['bankAccountNumber'])}'),
            trailing: Text(statusVi(w['status']))))).toList())),
    ]),
  );
}

class AddressesPage extends StatefulWidget {
  const AddressesPage({super.key});
  @override
  State<AddressesPage> createState() => _AddressesPageState();
}

class _AddressesPageState extends State<AddressesPage> {
  late Future<List<JsonMap>> rows = load();
  Future<List<JsonMap>> load() async => asList(await api.get('/api/v1/addresses'));
  void refresh() => setState(() => rows = load());
  Future<void> add() async {
    final name = await _ask(context, 'Tên người nhận'); if (name == null || !mounted) return;
    final phone = await _ask(context, 'Số điện thoại'); if (phone == null || !mounted) return;
    final province = await _ask(context, 'Tỉnh / thành phố'); if (province == null || !mounted) return;
    final ward = await _ask(context, 'Phường / xã'); if (ward == null || !mounted) return;
    final street = await _ask(context, 'Số nhà, đường'); if (street == null || !mounted) return;
    await runAction(context, () async {
      await api.post('/api/v1/addresses', {'recipientName': name,
        'recipientPhone': phone, 'province': province, 'ward': ward,
        'streetAddress': street});
      refresh();
    }, success: 'Đã lưu địa chỉ.');
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Sổ địa chỉ')),
    floatingActionButton: FloatingActionButton(onPressed: add, child: const Icon(Icons.add)),
    body: AsyncBody<List<JsonMap>>(future: rows, builder: (context, items) =>
      items.isEmpty ? const Center(child: Text('Chưa có địa chỉ.'))
      : ListView(children: items.map((a) => Card(child: ListTile(
        title: Text(str(a['recipientName'])),
        subtitle: Text('${a['recipientPhone']}\n${a['streetAddress']}, ${a['ward']}, ${a['province']}'),
        isThreeLine: true,
        trailing: IconButton(icon: const Icon(Icons.delete_outline),
          onPressed: () => runAction(context, () async {
            await api.delete('/api/v1/addresses/${a['id']}'); refresh();
          }))))).toList())),
  );
}

class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key});
  @override
  Widget build(BuildContext context) => ValueListenableBuilder<JsonMap?>(
    valueListenable: session, builder: (context, user, _) {
      if (user == null) return Center(child: Column(mainAxisSize: MainAxisSize.min,
        children: [const Icon(Icons.person_outline, size: 56, color: rose),
          const Text('Đăng nhập để quản lý tài khoản'),
          const SizedBox(height: 14),
          FilledButton(onPressed: () => needAccount(context),
              child: const Text('Đăng nhập / Đăng ký'))]));
      final roles = (user['roles'] is List ? user['roles'] as List : []);
      final permissions = (user['permissions'] is List ? user['permissions'] as List : []);
      final staff = roles.contains('Admin') || roles.contains('Inspector');
      Widget tile(IconData icon, String title, Widget page) => ListTile(
        leading: Icon(icon, color: rose), title: Text(title),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => openPage(context, page));
      return ListView(children: [
        ProfileHeader(user: user),
        if (user['mustChangePassword'] == true) Card(child: ListTile(
          title: const Text('Hãy đổi mật khẩu trước khi tiếp tục'),
          onTap: () => openPage(context, const ChangePasswordPage()))),
        const SectionTitle('Mua sắm'),
        tile(Icons.shopping_bag_outlined, 'Giỏ hàng',
            const ProductListPage(title: 'Giỏ hàng', path: '/api/v1/cart')),
        tile(Icons.favorite_border, 'Đồ đã thích',
            const ProductListPage(title: 'Đồ đã thích', path: '/api/v1/social/favorites')),
        tile(Icons.receipt_long_outlined, 'Đơn hàng của tôi', const OrdersPage()),
        tile(Icons.local_offer_outlined, 'Đề xuất giá', const OfferListPage()),
        const SectionTitle('Ký gửi'),
        tile(Icons.checkroom_outlined, 'Tủ đồ của tôi',
            const ProductListPage(title: 'Tủ đồ của tôi', path: '/api/v1/listings/mine')),
        tile(Icons.account_balance_wallet_outlined, 'Ví ký gửi', const WalletPage()),
        const SectionTitle('Liên hệ & cài đặt'),
        tile(Icons.forum_outlined, 'Tin nhắn', const ConversationListPage()),
        tile(Icons.support_agent, 'Hỗ trợ', const SupportPage()),
        tile(Icons.location_on_outlined, 'Sổ địa chỉ', const AddressesPage()),
        tile(Icons.lock_outline, 'Đổi mật khẩu', const ChangePasswordPage()),
        if (staff) ...[
          const SectionTitle('Nhân viên CoCo'),
          tile(Icons.verified_user_outlined, 'Kiểm định & quản trị',
              AdminPage(permissions: permissions.map(str).toSet())),
        ],
        Padding(padding: const EdgeInsets.all(18), child: OutlinedButton(
          onPressed: () => runAction(context, () async {
            await api.logout(); session.value = null;
          }, success: 'Đã đăng xuất.'), child: const Text('Đăng xuất'))),
      ]);
    },
  );
}

class ProfileHeader extends StatefulWidget {
  const ProfileHeader({super.key, required this.user});
  final JsonMap user;
  @override
  State<ProfileHeader> createState() => _ProfileHeaderState();
}

class _ProfileHeaderState extends State<ProfileHeader> {
  late Future<JsonMap> profile = api.get('/api/v1/mobile/profile').then(asMap);
  int version = 0;
  Future<void> updateAvatar() async {
    final photo = await ImagePicker().pickImage(source: ImageSource.gallery,
        imageQuality: 85);
    if (photo == null || !mounted) return;
    await runAction(context, () async {
      await api.multipart('/api/v1/mobile/profile/avatar', {}, [photo], 'image');
      setState(() {
        version = DateTime.now().millisecondsSinceEpoch;
        profile = api.get('/api/v1/mobile/profile').then(asMap);
      });
    }, success: 'Đã thay ảnh đại diện.');
  }
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(18), color: softRose,
    child: AsyncBody<JsonMap>(future: profile, builder: (context, p) => Row(children: [
      InkWell(onTap: updateAvatar, child: CircleAvatar(radius: 30,
        backgroundColor: rose,
        backgroundImage: p['hasAvatar'] == true
            ? NetworkImage('${api.baseUrl}/avatars/${widget.user['id']}?v=$version') : null,
        child: p['hasAvatar'] == true ? null : Text(
            str(widget.user['fullName']).isEmpty ? 'C' :
                str(widget.user['fullName'])[0].toUpperCase(),
            style: const TextStyle(color: Colors.white, fontSize: 24)))),
      const SizedBox(width: 14),
      Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(str(widget.user['fullName']), style: const TextStyle(
              fontSize: 19, fontWeight: FontWeight.w800)),
          Text(str(widget.user['email']), style: const TextStyle(color: Colors.black54)),
          Text('Điểm tín nhiệm: ${p['trustScore']}',
              style: const TextStyle(color: rose, fontWeight: FontWeight.w700)),
          TextButton(onPressed: updateAvatar, child: const Text('Thay ảnh đại diện')),
        ])),
    ])),
  );
}
