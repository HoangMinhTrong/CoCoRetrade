import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import 'api.dart';
import 'social.dart';
import 'ui.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});
  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  late Future<List<JsonMap>> products = load();
  Future<List<JsonMap>> load() async => asList(await api.get('/api/v1/products'));
  @override
  Widget build(BuildContext context) => RefreshIndicator(
    onRefresh: () async { setState(() => products = load()); await products; },
    child: ListView(children: [
      Container(margin: const EdgeInsets.fromLTRB(16, 10, 16, 0),
        padding: const EdgeInsets.all(22),
        decoration: BoxDecoration(color: ink, borderRadius: BorderRadius.circular(22)),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('COCO RETRADE', style: TextStyle(color: Color(0xffff9fb6),
              letterSpacing: 2, fontWeight: FontWeight.w800)),
          const SizedBox(height: 7),
          const Text('Món đồ đẹp, thêm một vòng đời mới.',
              style: TextStyle(color: Colors.white, fontSize: 23,
                  fontWeight: FontWeight.w800, height: 1.2)),
          const SizedBox(height: 8),
          Text('Thời trang tuần hoàn có kiểm định',
              style: TextStyle(color: Colors.white.withValues(alpha: .78))),
        ])),
      const SectionTitle('Mới lên kệ', subtitle: 'Từng món đồ đều được CoCo kiểm định'),
      AsyncBody<List<JsonMap>>(future: products, builder: (context, rows) => rows.isEmpty
        ? const Center(child: Padding(padding: EdgeInsets.all(36), child:
            Text('Chưa có sản phẩm đang mở bán.')))
        : Padding(padding: const EdgeInsets.symmetric(horizontal: 12), child:
            GridView.builder(shrinkWrap: true, physics: const NeverScrollableScrollPhysics(),
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2, childAspectRatio: .58,
                    crossAxisSpacing: 6, mainAxisSpacing: 6),
                itemCount: rows.length, itemBuilder: (context, i) => ProductTile(
                    product: rows[i], onTap: () => openPage(context,
                        ProductPage(id: str(rows[i]['id']))))))),
      const SizedBox(height: 24),
    ]));
}

class ShopPage extends StatefulWidget {
  const ShopPage({super.key});
  @override
  State<ShopPage> createState() => _ShopPageState();
}

class _ShopPageState extends State<ShopPage> {
  final search = TextEditingController();
  final brand = TextEditingController();
  final minPrice = TextEditingController();
  final maxPrice = TextEditingController();
  String category = '';
  String size = '';
  String sort = 'newest';
  int minCondition = 0;
  String fulfillment = '';
  late Future<List<JsonMap>> products = load();
  Future<List<JsonMap>> load() async => asList(await api.get('/api/v1/products', {
    if (search.text.trim().isNotEmpty) 'search': search.text.trim(),
    if (category.isNotEmpty) 'category': category,
    if (brand.text.trim().isNotEmpty) 'brand': brand.text.trim(),
    if (size.isNotEmpty) 'size': size,
    if (minPrice.text.isNotEmpty) 'minPrice': minPrice.text,
    if (maxPrice.text.isNotEmpty) 'maxPrice': maxPrice.text,
    if (minCondition > 0) 'minCondition': '$minCondition',
    if (fulfillment.isNotEmpty) 'fulfillment': fulfillment,
    'sort': sort,
  }));
  void refresh() => setState(() => products = load());
  @override
  void dispose() {
    for (final c in [search, brand, minPrice, maxPrice]) { c.dispose(); }
    super.dispose();
  }

  void filters() => showModalBottomSheet<void>(context: context, isScrollControlled: true,
    builder: (sheet) => Padding(padding: EdgeInsets.fromLTRB(20, 20, 20,
        MediaQuery.viewInsetsOf(sheet).bottom + 24), child:
      SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text('Bộ lọc', style: Theme.of(sheet).textTheme.titleLarge),
        const SizedBox(height: 14),
        TextField(controller: brand, decoration: const InputDecoration(labelText: 'Thương hiệu')),
        const SizedBox(height: 9),
        TextField(controller: minPrice, keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Giá từ (VNĐ)')),
        const SizedBox(height: 9),
        TextField(controller: maxPrice, keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Giá đến (VNĐ)')),
        const SizedBox(height: 9),
        DropdownButtonFormField<String>(initialValue: size,
          decoration: const InputDecoration(labelText: 'Kích cỡ'),
          items: ['', 'XS', 'S', 'M', 'L', 'XL', 'Free size'].map((v) =>
              DropdownMenuItem(value: v, child: Text(v.isEmpty ? 'Tất cả' : v))).toList(),
          onChanged: (v) => size = v ?? ''),
        const SizedBox(height: 9),
        DropdownButtonFormField<String>(initialValue: sort,
          decoration: const InputDecoration(labelText: 'Sắp xếp'),
          items: const [DropdownMenuItem(value: 'newest', child: Text('Mới nhất')),
            DropdownMenuItem(value: 'price_asc', child: Text('Giá tăng dần')),
            DropdownMenuItem(value: 'price_desc', child: Text('Giá giảm dần')),
            DropdownMenuItem(value: 'popular', child: Text('Yêu thích nhất'))],
          onChanged: (v) => sort = v ?? 'newest'),
        const SizedBox(height: 9),
        DropdownButtonFormField<int>(initialValue: minCondition,
          decoration: const InputDecoration(labelText: 'Độ mới tối thiểu'),
          items: const [DropdownMenuItem(value: 0, child: Text('Tất cả')),
            DropdownMenuItem(value: 90, child: Text('90% trở lên')),
            DropdownMenuItem(value: 95, child: Text('95% trở lên')),
            DropdownMenuItem(value: 99, child: Text('99% trở lên'))],
          onChanged: (v) => minCondition = v ?? 0),
        const SizedBox(height: 9),
        DropdownButtonFormField<String>(initialValue: fulfillment,
          decoration: const InputDecoration(labelText: 'Cách nhận'),
          items: const [DropdownMenuItem(value: '', child: Text('Tất cả')),
            DropdownMenuItem(value: 'pickup', child: Text('Lấy tại CoCo')),
            DropdownMenuItem(value: 'shipping', child: Text('Giao tận nơi'))],
          onChanged: (v) => fulfillment = v ?? ''),
        const SizedBox(height: 18),
        FilledButton(onPressed: () { Navigator.pop(sheet); refresh(); },
            child: const Text('Áp dụng')),
      ]))));

  @override
  Widget build(BuildContext context) => Column(children: [
    Padding(padding: const EdgeInsets.all(16), child: Row(children: [
      Expanded(child: TextField(controller: search, onSubmitted: (_) => refresh(),
          decoration: const InputDecoration(prefixIcon: Icon(Icons.search),
              hintText: 'Tên món đồ, thương hiệu, SKU'))),
      IconButton(onPressed: filters, icon: const Icon(Icons.tune), tooltip: 'Bộ lọc'),
    ])),
    SizedBox(height: 46, child: ListView(scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 14), children: [
        for (final c in ['', 'Áo', 'Quần', 'Váy', 'Túi', 'Phụ kiện'])
          Padding(padding: const EdgeInsets.only(right: 6), child: ChoiceChip(
            label: Text(c.isEmpty ? 'Tất cả' : c), selected: category == c,
            onSelected: (_) { category = c; refresh(); })),
      ])),
    Expanded(child: AsyncBody<List<JsonMap>>(future: products,
        builder: (context, rows) => rows.isEmpty
          ? const Center(child: Text('Không tìm thấy sản phẩm phù hợp.'))
          : GridView.builder(padding: const EdgeInsets.all(12),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 2, childAspectRatio: .58,
                  crossAxisSpacing: 6, mainAxisSpacing: 6),
              itemCount: rows.length, itemBuilder: (context, i) => ProductTile(
                  product: rows[i], onTap: () => openPage(context,
                      ProductPage(id: str(rows[i]['id']))))))),
  ]);
}

class ProductPage extends StatefulWidget {
  const ProductPage({super.key, required this.id, this.ownListing = false});
  final String id;
  final bool ownListing;
  @override
  State<ProductPage> createState() => _ProductPageState();
}

class _ProductPageState extends State<ProductPage> {
  late Future<JsonMap> product = load();
  Future<JsonMap> load() async => asMap(await api.get(
      '${widget.ownListing ? '/api/v1/listings' : '/api/v1/products'}/${widget.id}'));
  void reload() => setState(() => product = load());
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Chi tiết sản phẩm')),
    body: AsyncBody<JsonMap>(future: product, builder: (context, p) {
      final images = p['imageIds'] is List ? p['imageIds'] as List : [];
      final active = str(p['status']) == 'Active';
      final ownProduct = str(p['sellerId']) == str(session.value?['id']);
      return ListView(children: [
        SizedBox(height: 360, child: PageView(children: images.isEmpty
            ? [productImage(null)] : images.map((id) => productImage(id)).toList())),
        Padding(padding: const EdgeInsets.all(20), child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(active ? '✓ ĐÃ KIỂM ĐỊNH BỞI COCO' : statusVi(p['status']),
                style: const TextStyle(color: rose, fontSize: 12, fontWeight: FontWeight.w800)),
            const SizedBox(height: 8),
            Text(str(p['name']), style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w800)),
            const SizedBox(height: 8),
            Text(money(p['askingPrice']), style: const TextStyle(
                color: rose, fontSize: 26, fontWeight: FontWeight.w900)),
            const Divider(height: 28),
            labeled('Thương hiệu', str(p['brand'])),
            labeled('Kích cỡ', str(p['size'])),
            labeled('Độ mới', '${p['verifiedCondition'] ?? p['declaredCondition']}%'),
            labeled(active ? 'Mã sản phẩm' : 'Mã ký gửi',
                active ? str(p['sku']) : str(p['consignmentCode'])),
            if (!active && str(p['rejectionReason']).isNotEmpty)
              labeled('Lý do từ chối', str(p['rejectionReason'])),
            if (active && !ownProduct) ...[
            ListTile(contentPadding: EdgeInsets.zero,
              leading: CircleAvatar(backgroundImage: NetworkImage(
                  '${api.baseUrl}/avatars/${p['sellerId']}'),
                  onBackgroundImageError: (_, _) {}),
              title: Text(str(p['sellerName']).isEmpty ? 'Người ký gửi CoCo'
                  : str(p['sellerName'])),
              subtitle: const Text('Tủ đồ người bán'),
              trailing: TextButton(onPressed: () async {
                if (!await needAccount(context) || !context.mounted) return;
                await runAction(context, () async =>
                    api.put('/api/v1/social/following/${p['sellerId']}'),
                    success: 'Đã theo dõi tủ đồ.');
              }, child: const Text('Theo dõi')),
              onTap: () => openPage(context, SellerClosetPage(
                  sellerId: str(p['sellerId']), sellerName: str(p['sellerName'])))),
            const SizedBox(height: 14),
            FilledButton(onPressed: () async {
              if (!await needAccount(context) || !context.mounted) return;
              openPage(context, CheckoutPage(productId: widget.id));
            }, child: const Text('Mua ngay')),
            const SizedBox(height: 8),
            if (p['allowOffers'] == true) OutlinedButton(
                onPressed: () async {
                  if (!await needAccount(context) || !context.mounted) return;
                  final field = TextEditingController();
                  final price = await showDialog<String>(context: context,
                    builder: (dialog) => AlertDialog(title: const Text('Đề xuất giá'),
                      content: TextField(controller: field, keyboardType: TextInputType.number,
                          decoration: const InputDecoration(labelText: 'Giá bạn muốn trả (VNĐ)')),
                      actions: [TextButton(onPressed: () => Navigator.pop(dialog),
                          child: const Text('Đóng')),
                        FilledButton(onPressed: () => Navigator.pop(dialog, field.text),
                            child: const Text('Gửi đề xuất'))]));
                  field.dispose();
                  if (price == null || !context.mounted) return;
                  final amount = num.tryParse(price);
                  if (amount == null) { flash(context, 'Giá trả phải là số VNĐ hợp lệ.'); return; }
                  await runAction(context, () async {
                    final offer = asMap(await api.post('/api/v1/products/${widget.id}/offers',
                        {'price': amount}));
                    if (context.mounted) openPage(context, OfferPage(id: str(offer['id'])));
                  });
                }, child: const Text('Trả giá')),
            Row(children: [
              Expanded(child: TextButton.icon(onPressed: () async {
                if (!await needAccount(context) || !context.mounted) return;
                await runAction(context, () async => api.put('/api/v1/cart/items/${widget.id}'),
                    success: 'Đã thêm vào giỏ hàng.');
              }, icon: const Icon(Icons.shopping_bag_outlined), label: const Text('Thêm vào giỏ'))),
              Expanded(child: TextButton.icon(onPressed: () async {
                if (!await needAccount(context) || !context.mounted) return;
                await runAction(context, () async => api.put('/api/v1/social/favorites/${widget.id}'),
                    success: 'Đã lưu sản phẩm.');
              }, icon: const Icon(Icons.favorite_border), label: const Text('Yêu thích'))),
            ]),
            TextButton.icon(onPressed: () async {
              if (!await needAccount(context) || !context.mounted) return;
              await runAction(context, () async {
                final result = asMap(await api.post('/api/v1/messages/products/${widget.id}'));
                if (context.mounted) openPage(context, ConversationPage(id: str(result['id'])));
              });
            }, icon: const Icon(Icons.chat_bubble_outline),
                label: const Text('Nhắn người bán')),
            ],
          ]))]);
    }),
  );
}

class OfferPage extends StatefulWidget {
  const OfferPage({super.key, required this.id});
  final String id;
  @override
  State<OfferPage> createState() => _OfferPageState();
}

class _OfferPageState extends State<OfferPage> {
  late Future<JsonMap> offer = load();
  Future<JsonMap> load() async => asMap(await api.get('/api/v1/offers/${widget.id}'));
  void reload() => setState(() => offer = load());
  Future<void> answer(String action, [num? price]) async {
    await runAction(context, () async {
      await api.post('/api/v1/offers/${widget.id}/$action',
          price == null ? null : {'price': price});
      reload();
    }, success: 'Đã cập nhật đề xuất giá.');
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Thỏa thuận giá')),
    body: AsyncBody<JsonMap>(future: offer, builder: (context, o) {
      final me = str(session.value?['id']);
      final isBuyer = me == str(o['buyerId']);
      final status = str(o['status']);
      final canAnswer = status == (isBuyer ? 'PendingBuyer' : 'PendingSeller');
      final events = asList(o['events']);
      return ListView(padding: const EdgeInsets.all(18), children: [
        Card(child: Padding(padding: const EdgeInsets.all(18), child: Column(children: [
          Text(str(o['productName']), style: Theme.of(context).textTheme.titleLarge),
          labeled('Giá niêm yết', money(o['askingPrice'])),
          labeled('Giá hiện tại', money(o['currentPrice']), valueColor: rose),
            labeled('Trạng thái', statusVi(status)),
          labeled('Hết hạn', localDate(o['expiresAtUtc'])),
          labeled('Lượt trả giá của bạn', '${o['buyerOffersUsed']}/3'),
        ]))),
        if (canAnswer) ...[
          const SizedBox(height: 10),
          FilledButton(onPressed: () => answer('accept'), child: const Text('Chấp nhận giá')),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () => answer('decline'), child: const Text('Từ chối')),
          TextButton(onPressed: () async {
            final field = TextEditingController();
            final value = await showDialog<String>(context: context,
                builder: (dialog) => AlertDialog(title: const Text('Đề xuất lại'),
                  content: TextField(controller: field, keyboardType: TextInputType.number,
                    decoration: InputDecoration(labelText: 'Giá từ ${money(o['counterMinimumPrice'])}'
                        ' đến ${money(o['counterMaximumPrice'])}')),
                  actions: [TextButton(onPressed: () => Navigator.pop(dialog), child: const Text('Đóng')),
                    FilledButton(onPressed: () => Navigator.pop(dialog, field.text),
                        child: const Text('Gửi'))]));
            field.dispose();
            final price = num.tryParse(value ?? '');
            if (price != null) await answer('counter', price);
          }, child: const Text('Đề xuất mức giá khác')),
        ],
        if (status == 'Accepted' && isBuyer) ...[
          const SizedBox(height: 10),
          FilledButton(onPressed: () => openPage(context, CheckoutPage(
              productId: str(o['productId']), offerId: widget.id)),
              child: const Text('Đặt mua theo giá đã chốt')),
          const Text('Giá thỏa thuận chỉ dành cho bạn trong 24 giờ. '
              'Món hàng chỉ được tạm giữ khi xác nhận đơn.',
              style: TextStyle(color: Colors.black54)),
        ],
        const SectionTitle('Lịch sử thương lượng'),
        for (final e in events) Card(child: ListTile(
          title: Text(statusVi(e['action'])), subtitle: Text(localDate(e['createdAtUtc'])),
          trailing: e['price'] == null ? null : Text(money(e['price'])))),
      ]);
    }),
  );
}

class CheckoutPage extends StatefulWidget {
  const CheckoutPage({super.key, required this.productId, this.offerId});
  final String productId;
  final String? offerId;
  @override
  State<CheckoutPage> createState() => _CheckoutPageState();
}

class _CheckoutPageState extends State<CheckoutPage> {
  final name = TextEditingController();
  final phone = TextEditingController();
  final province = TextEditingController();
  final ward = TextEditingController();
  final street = TextEditingController();
  final note = TextEditingController();
  final requestKey = newKey();
  String fulfillment = 'Pickup';
  String deliveryWindow = 'BusinessHours';
  DateTime? pickup;
  Future<JsonMap>? quote;
  bool sending = false;
  @override
  void initState() { super.initState(); refreshQuote(); }
  void refreshQuote() => setState(() => quote = api.get('/api/v1/orders/quote', {
    'productId': widget.productId,
    if (widget.offerId != null) 'offerId': widget.offerId!,
    'fulfillment': fulfillment,
    if (fulfillment == 'Shipping') 'province': province.text,
    if (fulfillment == 'Shipping') 'ward': ward.text,
  }).then(asMap));
  @override
  void dispose() {
    for (final c in [name, phone, province, ward, street, note]) { c.dispose(); }
    super.dispose();
  }
  String pickupString() {
    final d = pickup!;
    String n(int v) => v.toString().padLeft(2, '0');
    return '${d.year}-${n(d.month)}-${n(d.day)}T${n(d.hour)}:${n(d.minute)}';
  }
  Future<void> choosePickup() async {
    final vnNow = DateTime.now().toUtc().add(const Duration(hours: 7));
    final date = await showDatePicker(context: context,
        firstDate: DateTime(vnNow.year, vnNow.month, vnNow.day),
        lastDate: DateTime(vnNow.year, vnNow.month, vnNow.day).add(const Duration(days: 3)),
        initialDate: DateTime(vnNow.year, vnNow.month, vnNow.day).add(const Duration(days: 1)));
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context,
        initialTime: const TimeOfDay(hour: 10, minute: 0));
    if (time == null) return;
    setState(() => pickup = DateTime(date.year, date.month, date.day, time.hour, time.minute));
  }
  Future<void> submit() async {
    if (name.text.trim().length < 2 || !RegExp(r'^0[35789]\d{8}$').hasMatch(phone.text)) {
      flash(context, 'Nhập tên và số điện thoại Việt Nam hợp lệ.'); return;
    }
    if (fulfillment == 'Pickup' && pickup == null) {
      flash(context, 'Hãy chọn giờ lấy hàng tại CoCo.'); return;
    }
    if (fulfillment == 'Shipping' && (province.text.trim().isEmpty ||
        ward.text.trim().isEmpty || street.text.trim().isEmpty)) {
      flash(context, 'Nhập đầy đủ tỉnh/thành, phường/xã và địa chỉ.'); return;
    }
    setState(() => sending = true);
    try {
      final order = asMap(await api.post('/api/v1/orders', {
        'productId': widget.productId, 'offerId': widget.offerId,
        'idempotencyKey': requestKey, 'recipientName': name.text.trim(),
        'recipientPhone': phone.text.trim(), 'fulfillment': fulfillment,
        'pickupLocal': fulfillment == 'Pickup' ? pickupString() : null,
        'province': fulfillment == 'Shipping' ? province.text.trim() : null,
        'ward': fulfillment == 'Shipping' ? ward.text.trim() : null,
        'streetAddress': fulfillment == 'Shipping' ? street.text.trim() : null,
        'preferredDeliveryWindow': fulfillment == 'Shipping' ? deliveryWindow : null,
        'deliveryNote': fulfillment == 'Shipping' ? note.text.trim() : null,
      }));
      if (mounted) {
        Navigator.of(context).pushReplacement(MaterialPageRoute<void>(
            builder: (_) => OrderPage(id: str(order['id']))));
      }
    } on ApiException catch (ex) {
      if (mounted) flash(context, ex.message);
    } finally { if (mounted) setState(() => sending = false); }
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Xác nhận đặt hàng')),
    body: ListView(padding: const EdgeInsets.all(18), children: [
      const Text('Chọn cách nhận hàng', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 19)),
      RadioGroup<String>(groupValue: fulfillment, onChanged: (v) {
        if (v == null) return;
        fulfillment = v;
        refreshQuote();
      }, child: const Column(children: [
        RadioListTile<String>(title: Text('Tới CoCo lấy'),
            subtitle: Text('Miễn phí · hẹn giờ lấy'), value: 'Pickup'),
        RadioListTile<String>(title: Text('Giao hàng tận nơi'),
            subtitle: Text('Thanh toán khi nhận hàng'), value: 'Shipping'),
      ])),
      const SizedBox(height: 10),
      TextField(controller: name, decoration: const InputDecoration(labelText: 'Tên người nhận')),
      const SizedBox(height: 10),
      TextField(controller: phone, keyboardType: TextInputType.phone,
          decoration: const InputDecoration(labelText: 'Số điện thoại nhận hàng')),
      const SizedBox(height: 10),
      if (fulfillment == 'Pickup') OutlinedButton.icon(onPressed: choosePickup,
          icon: const Icon(Icons.schedule), label: Text(pickup == null
              ? 'Chọn ngày, giờ lấy hàng (giờ Việt Nam)'
              : 'Lấy lúc ${pickupString().replaceFirst('T', ' ')}')),
      if (fulfillment == 'Shipping') ...[
        OutlinedButton.icon(onPressed: () async {
          await runAction(context, () async {
            final saved = asList(await api.get('/api/v1/addresses'));
            if (!context.mounted) return;
            final chosen = await showModalBottomSheet<JsonMap>(context: context,
              builder: (sheet) => SafeArea(child: ListView(
                children: saved.map((a) => ListTile(
                  title: Text(str(a['recipientName'])),
                  subtitle: Text('${a['streetAddress']}, ${a['ward']}, ${a['province']}'),
                  onTap: () => Navigator.pop(sheet, a))).toList())));
            if (chosen == null || !mounted) return;
            setState(() {
              name.text = str(chosen['recipientName']);
              phone.text = str(chosen['recipientPhone']);
              province.text = str(chosen['province']);
              ward.text = str(chosen['ward']);
              street.text = str(chosen['streetAddress']);
            });
            refreshQuote();
          });
        }, icon: const Icon(Icons.bookmark_border),
            label: const Text('Chọn địa chỉ đã lưu')),
        const SizedBox(height: 10),
        TextField(controller: province, decoration: const InputDecoration(labelText: 'Tỉnh / thành phố'),
            onChanged: (_) => refreshQuote()),
        const SizedBox(height: 10),
        TextField(controller: ward, decoration: const InputDecoration(labelText: 'Phường / xã'),
            onChanged: (_) => refreshQuote()),
        const SizedBox(height: 10),
        TextField(controller: street, decoration: const InputDecoration(labelText: 'Số nhà, đường')),
        const SizedBox(height: 10),
        DropdownButtonFormField<String>(initialValue: deliveryWindow,
          decoration: const InputDecoration(labelText: 'Khung giờ giao thuận tiện'),
          items: const [DropdownMenuItem(value: 'Morning', child: Text('Buổi sáng')),
            DropdownMenuItem(value: 'Afternoon', child: Text('Buổi chiều')),
            DropdownMenuItem(value: 'BusinessHours', child: Text('Giờ hành chính'))],
          onChanged: (v) => deliveryWindow = v ?? 'BusinessHours'),
        const SizedBox(height: 10),
        TextField(controller: note, decoration: const InputDecoration(labelText: 'Ghi chú cho người giao')),
      ],
      const SizedBox(height: 15),
      if (quote != null) AsyncBody<JsonMap>(future: quote!, builder: (context, q) =>
        Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(children: [
          Text(str(q['productName']), style: const TextStyle(fontWeight: FontWeight.w800)),
          labeled('Tiền hàng', money(q['merchandisePrice'])),
          labeled('Phí vận chuyển', q['shippingFeePending'] == true
              ? 'Nhập tỉnh/thành và phường/xã' : money(q['shippingFee'])),
          const Divider(),
          labeled('Tổng thanh toán', q['shippingFeePending'] == true
              ? 'Chờ địa chỉ' : money(q['totalAmount']), valueColor: rose),
          const Text('Thanh toán tiền mặt khi nhận hàng. Không chuyển khoản trước.',
              style: TextStyle(color: Colors.black54)),
        ])))),
      const SizedBox(height: 16),
      FilledButton(onPressed: sending ? null : submit,
          child: Text(sending ? 'Đang đặt hàng...' : 'Xác nhận đặt hàng')),
    ]),
  );
}

class ListingPage extends StatefulWidget {
  const ListingPage({super.key});
  @override
  State<ListingPage> createState() => _ListingPageState();
}

class SellerClosetPage extends StatelessWidget {
  const SellerClosetPage({super.key, required this.sellerId, required this.sellerName});
  final String sellerId;
  final String sellerName;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(sellerName.isEmpty ? 'Tủ đồ người bán' : sellerName)),
    body: AsyncBody<List<JsonMap>>(future: api.get('/api/v1/products',
        {'sellerId': sellerId}).then(asList),
      builder: (context, rows) => rows.isEmpty
        ? const Center(child: Text('Chưa có sản phẩm đang mở bán.'))
        : GridView.builder(padding: const EdgeInsets.all(12), itemCount: rows.length,
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 2, childAspectRatio: .58,
                crossAxisSpacing: 6, mainAxisSpacing: 6),
            itemBuilder: (context, i) => ProductTile(product: rows[i],
                onTap: () => openPage(context, ProductPage(id: str(rows[i]['id'])))))),
  );
}

class _ListingPageState extends State<ListingPage> {
  final name = TextEditingController(); final brand = TextEditingController();
  final size = TextEditingController(); final original = TextEditingController();
  final asking = TextEditingController();
  String category = 'Áo'; int condition = 95; bool offers = true;
  List<XFile> photos = []; bool sending = false;
  @override
  void dispose() { for (final c in [name, brand, size, original, asking]) { c.dispose(); } super.dispose(); }
  Future<void> pick() async {
    final selected = await ImagePicker().pickMultiImage(imageQuality: 90);
    if (selected.isNotEmpty) setState(() => photos = [...photos, ...selected].take(10).toList());
  }
  Future<void> takePhoto() async {
    if (photos.length >= 10) {
      flash(context, 'Tối đa 10 ảnh cho một sản phẩm.'); return;
    }
    final photo = await ImagePicker().pickImage(source: ImageSource.camera,
        imageQuality: 90);
    if (photo != null && mounted) setState(() => photos = [...photos, photo]);
  }
  Future<void> submit() async {
    if (!await needAccount(context) || !mounted) return;
    if (photos.length < 3 || photos.length > 10) { flash(context, 'Chọn từ 3 đến 10 ảnh.'); return; }
    for (final f in photos) {
      if (!RegExp(r'\.(jpe?g|png|webp)$', caseSensitive: false).hasMatch(f.name)
          || await f.length() > 5000000) {
        if (mounted) flash(context, 'Chỉ nhận JPG, PNG, WebP; mỗi ảnh tối đa 5 MB.');
        return;
      }
    }
    if (name.text.trim().length < 3 || brand.text.trim().length < 2 ||
        size.text.trim().isEmpty || (num.tryParse(original.text) ?? 0) < 1000 ||
        (num.tryParse(asking.text) ?? 0) < 1000) {
      flash(context, 'Nhập đầy đủ thông tin; giá tính bằng VNĐ, tối thiểu 1,000.'); return;
    }
    setState(() => sending = true);
    try {
      final result = asMap(await api.multipart('/api/v1/listings', {
        'Name': name.text.trim(), 'Brand': brand.text.trim(),
        'Category': category, 'Size': size.text.trim(),
        'OriginalPrice': original.text, 'AskingPrice': asking.text,
        'ConditionPercent': '$condition', 'AllowOffers': '$offers',
      }, photos, 'Images'));
      if (mounted) {
        flash(context, 'Đã gửi kiểm định. Mã ký gửi: ${result['consignmentCode']}');
        Navigator.pop(context);
      }
    } on ApiException catch (ex) { if (mounted) flash(context, ex.message); }
    finally { if (mounted) setState(() => sending = false); }
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Ký gửi cùng CoCo')),
    body: ListView(padding: const EdgeInsets.all(18), children: [
      const Text('Ảnh thật của sản phẩm', style: TextStyle(fontWeight: FontWeight.w800)),
      const SizedBox(height: 6),
      const Text('Tối thiểu 3 ảnh: mặt trước, tem/mác và chi tiết tình trạng.',
          style: TextStyle(color: Colors.black54)),
      OutlinedButton.icon(onPressed: pick, icon: const Icon(Icons.add_photo_alternate_outlined),
          label: Text('Chọn ảnh (${photos.length}/10)')),
      OutlinedButton.icon(onPressed: takePhoto, icon: const Icon(Icons.camera_alt_outlined),
          label: const Text('Chụp ảnh sản phẩm')),
      if (photos.isNotEmpty) SizedBox(height: 90, child: ListView(scrollDirection: Axis.horizontal,
        children: photos.map((f) => Padding(padding: const EdgeInsets.only(right: 8),
            child: FutureBuilder(future: f.readAsBytes(), builder: (context, s) =>
                s.hasData ? Image.memory(s.data!, width: 80, fit: BoxFit.cover)
                    : const SizedBox(width: 80, child: CircularProgressIndicator())))).toList())),
      const SizedBox(height: 12),
      TextField(controller: name, decoration: const InputDecoration(labelText: 'Tên sản phẩm')),
      const SizedBox(height: 10),
      TextField(controller: brand, decoration: const InputDecoration(labelText: 'Thương hiệu')),
      const SizedBox(height: 10),
      DropdownButtonFormField<String>(initialValue: category,
        decoration: const InputDecoration(labelText: 'Danh mục'),
        items: ['Áo', 'Quần', 'Váy', 'Túi', 'Phụ kiện'].map((v) =>
            DropdownMenuItem(value: v, child: Text(v))).toList(),
        onChanged: (v) => category = v ?? 'Áo'),
      const SizedBox(height: 10),
      TextField(controller: size, decoration: const InputDecoration(labelText: 'Kích cỡ')),
      const SizedBox(height: 10),
      TextField(controller: original, keyboardType: TextInputType.number,
          decoration: const InputDecoration(labelText: 'Giá gốc (VNĐ)', hintText: 'Ví dụ: 29000')),
      const SizedBox(height: 10),
      TextField(controller: asking, keyboardType: TextInputType.number,
          decoration: const InputDecoration(labelText: 'Giá muốn bán (VNĐ)')),
      const SizedBox(height: 12),
      Text('Độ mới: $condition%'),
      Slider(value: condition.toDouble(), min: 50, max: 100, divisions: 50,
          onChanged: (v) => setState(() => condition = v.round())),
      SwitchListTile(value: offers, title: const Text('Cho phép trả giá'),
          onChanged: (v) => setState(() => offers = v)),
      const SizedBox(height: 10),
      FilledButton(onPressed: sending ? null : submit,
          child: Text(sending ? 'Đang gửi...' : 'Gửi kiểm định')),
      const SizedBox(height: 16),
    ]),
  );
}
