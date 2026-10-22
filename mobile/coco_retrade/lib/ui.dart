import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import 'api.dart';
import 'auth.dart';

final api = CoCoApi();
final session = ValueNotifier<JsonMap?>(null);

const rose = Color(0xffb62549);
const ink = Color(0xff262424);
const cream = Color(0xfffcfaf9);
const softRose = Color(0xfffff0f3);

ThemeData cocoTheme() => ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(seedColor: rose, primary: rose,
          surface: Colors.white, onSurface: ink),
      scaffoldBackgroundColor: cream,
      appBarTheme: const AppBarTheme(backgroundColor: cream,
          foregroundColor: ink, centerTitle: false, elevation: 0),
      cardTheme: CardThemeData(color: Colors.white, elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18),
              side: const BorderSide(color: Color(0xfff0e8e8)))),
      inputDecorationTheme: InputDecorationTheme(
        filled: true, fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: Color(0xffeadfe2))),
      ),
      filledButtonTheme: FilledButtonThemeData(style: FilledButton.styleFrom(
          backgroundColor: rose, foregroundColor: Colors.white,
          minimumSize: const Size(0, 48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(13)))),
    );

class Brand extends StatelessWidget {
  const Brand({super.key, this.height = 47});
  final double height;
  @override
  Widget build(BuildContext context) => SvgPicture.asset(
      'assets/coco-logo.svg', height: height, semanticsLabel: 'CoCo ReTrade');
}

void openPage(BuildContext context, Widget page) => Navigator.of(context)
    .push(MaterialPageRoute<void>(builder: (_) => page));

Future<bool> needAccount(BuildContext context) async {
  if (session.value != null) return true;
  final result = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => const AuthPage()));
  return result == true && session.value != null;
}

void flash(BuildContext context, String message) {
  if (!context.mounted) return;
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
}

Future<void> runAction(BuildContext context, Future<dynamic> Function() action,
    {String? success}) async {
  try {
    await action();
    if (success != null && context.mounted) flash(context, success);
  } on ApiException catch (ex) {
    if (context.mounted) flash(context, ex.message);
  } catch (_) {
    if (context.mounted) flash(context, 'Không kết nối được với CoCo. Hãy kiểm tra mạng.');
  }
}

class AsyncBody<T> extends StatelessWidget {
  const AsyncBody({super.key, required this.future, required this.builder,
    this.emptyText = 'Chưa có dữ liệu.'});
  final Future<T> future;
  final Widget Function(BuildContext, T) builder;
  final String emptyText;
  @override
  Widget build(BuildContext context) => FutureBuilder<T>(future: future,
      builder: (context, snap) {
        if (!snap.hasData && !snap.hasError) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          return Center(child: Padding(padding: const EdgeInsets.all(24),
            child: Text(snap.error is ApiException
                ? (snap.error as ApiException).message
                : 'Không tải được dữ liệu. Kiểm tra kết nối rồi thử lại.',
                textAlign: TextAlign.center)));
        }
        return builder(context, snap.data as T);
      });
}

class SectionTitle extends StatelessWidget {
  const SectionTitle(this.title, {super.key, this.subtitle});
  final String title;
  final String? subtitle;
  @override
  Widget build(BuildContext context) => Padding(
      padding: const EdgeInsets.fromLTRB(18, 22, 18, 12),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title, style: Theme.of(context).textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.w800)),
        if (subtitle != null) Text(subtitle!, style: const TextStyle(color: Colors.black54)),
      ]));
}

Widget productImage(dynamic id, {double? height, BoxFit fit = BoxFit.cover}) {
  if (str(id).isEmpty || str(id) == 'null') {
    return Container(height: height, color: softRose,
        child: const Center(child: Icon(Icons.checkroom, size: 44, color: rose)));
  }
  return Image.network(api.imageUrl(id), height: height, fit: fit,
      headers: api.imageHeaders,
      errorBuilder: (_, _, _) => Container(height: height, color: softRose,
          child: const Center(child: Icon(Icons.broken_image_outlined, color: rose))));
}

class ProductTile extends StatelessWidget {
  const ProductTile({super.key, required this.product, required this.onTap});
  final JsonMap product;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => InkWell(
    borderRadius: BorderRadius.circular(18), onTap: onTap,
    child: Card(clipBehavior: Clip.antiAlias, child: Column(
      crossAxisAlignment: CrossAxisAlignment.start, children: [
        AspectRatio(aspectRatio: .88, child: SizedBox.expand(child:
            productImage(product['coverImageId']))),
        Padding(padding: const EdgeInsets.all(11), child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('ĐÃ KIỂM ĐỊNH', style: Theme.of(context).textTheme.labelSmall
                ?.copyWith(color: rose, fontWeight: FontWeight.w800)),
            const SizedBox(height: 4),
            Text(str(product['name']), maxLines: 1, overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontWeight: FontWeight.w700)),
            Text('${str(product['brand'])} · ${str(product['size'])}',
                maxLines: 1, overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, color: Colors.black54)),
            const SizedBox(height: 6),
            Text(money(product['askingPrice']),
                style: const TextStyle(fontWeight: FontWeight.w800, color: rose)),
          ])),
      ])),
  );
}

Widget labeled(String label, String value, {Color? valueColor}) => Padding(
  padding: const EdgeInsets.symmetric(vertical: 7),
  child: Row(children: [Expanded(child: Text(label,
      style: const TextStyle(color: Colors.black54))),
    Flexible(child: Text(value, textAlign: TextAlign.end,
      style: TextStyle(fontWeight: FontWeight.w700, color: valueColor)))]),
);

String localDate(dynamic utc) {
  final date = DateTime.tryParse(str(utc));
  if (date == null) return '—';
  final d = date.toUtc().add(const Duration(hours: 7));
  String n(int v) => v.toString().padLeft(2, '0');
  return '${n(d.day)}/${n(d.month)}/${d.year} ${n(d.hour)}:${n(d.minute)}';
}

String statusVi(dynamic value) => switch (str(value)) {
  'PendingApproval' => 'Chờ kiểm định',
  'Active' => 'Đang mở bán',
  'Reserved' => 'Đang tạm giữ',
  'Sold' => 'Đã bán',
  'PendingSeller' => 'Chờ người bán phản hồi',
  'PendingBuyer' => 'Chờ người mua phản hồi',
  'Accepted' => 'Đã chấp nhận',
  'Declined' => 'Đã từ chối',
  'Expired' => 'Đã hết hạn',
  'Used' => 'Đã dùng để đặt hàng',
  'PendingProcessing' => 'Chờ xử lý',
  'Shipping' => 'Đang giao',
  'ReadyForPickup' => 'Sẵn sàng để lấy',
  'Completed' => 'Hoàn tất',
  'Cancelled' => 'Đã hủy',
  'Unpaid' => 'Chưa thu tiền',
  'Paid' => 'Đã thu tiền',
  'Pending' => 'Chờ duyệt',
  'Approved' => 'Đã duyệt',
  'Transferred' => 'Đã xác nhận chuyển khoản',
  'Closed' => 'Đã đóng',
  'Open' => 'Đang mở',
  'Submitted' => 'Đã gửi trả giá',
  'SellerCountered' => 'Người bán đề xuất lại',
  'BuyerCountered' => 'Người mua đề xuất lại',
  'Ordered' => 'Đã đặt hàng',
  _ => str(value),
};
