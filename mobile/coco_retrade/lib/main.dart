import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'market.dart';
import 'social.dart';
import 'ui.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const CoCoMobileApp());
}

class CoCoMobileApp extends StatelessWidget {
  const CoCoMobileApp({super.key});
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'CoCo ReTrade', theme: cocoTheme(), debugShowCheckedModeBanner: false,
    home: const StartupPage(),
  );
}

class StartupPage extends StatefulWidget {
  const StartupPage({super.key});
  @override
  State<StartupPage> createState() => _StartupPageState();
}

class _StartupPageState extends State<StartupPage> {
  bool ready = false;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    try {
      await api.init();
      session.value = asMap(await api.get('/api/v1/mobile/account/me')
          .timeout(const Duration(seconds: 10)));
    } catch (_) {
      // The public catalog can still render its own connection error.
    }
    if (mounted) setState(() => ready = true);
  }
  @override
  Widget build(BuildContext context) {
    if (ready) return const CoCoShell();
    return Scaffold(body: SafeArea(child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Center(child: Brand(height: 90)),
        const SizedBox(height: 20),
        const Center(child: Text('Thời trang tuần hoàn có kiểm định')),
        const SizedBox(height: 32),
        for (var i = 0; i < 3; i++) Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Container(height: i == 0 ? 110 : 74,
            decoration: BoxDecoration(color: softRose,
                borderRadius: BorderRadius.circular(16)))),
      ]))));
  }
}

class CoCoShell extends StatefulWidget {
  const CoCoShell({super.key});
  @override
  State<CoCoShell> createState() => _CoCoShellState();
}

class _CoCoShellState extends State<CoCoShell> {
  int index = 0;
  Timer? unreadTimer;
  JsonMap counts = {};
  @override
  void initState() {
    super.initState();
    fetchCounts();
    unreadTimer = Timer.periodic(const Duration(seconds: 15), (_) => fetchCounts());
  }
  Future<void> fetchCounts() async {
    if (session.value == null) return;
    try {
      final result = asMap(await api.get('/api/v1/unread-counts'));
      if (mounted) setState(() => counts = result);
    } catch (_) { /* Badges are optional when offline. */ }
  }
  @override
  void dispose() { unreadTimer?.cancel(); super.dispose(); }
  @override
  Widget build(BuildContext context) => ValueListenableBuilder<JsonMap?>(
    valueListenable: session, builder: (context, user, _) {
      final pages = <Widget>[
        const HomePage(), const ShopPage(), const SizedBox.shrink(),
        const NotificationsPage(), const ProfilePage(),
      ];
      Widget badge(IconData icon, int count) => count > 0
          ? Badge.count(count: count, child: Icon(icon)) : Icon(icon);
      return Scaffold(
        appBar: AppBar(title: index == 0 ? const Brand(height: 43)
          : Text(['Trang chủ', 'Khám phá', 'Ký gửi', 'Thông báo', 'Tôi'][index]),
          actions: [
            IconButton(tooltip: 'Tin nhắn',
              icon: badge(Icons.chat_bubble_outline, number(counts['messages']).toInt()),
              onPressed: () async {
                if (await needAccount(context) && context.mounted) {
                  openPage(context, const ConversationListPage());
                }
              }),
            IconButton(tooltip: 'Giỏ hàng', icon: const Icon(Icons.shopping_bag_outlined),
              onPressed: () async {
                if (await needAccount(context) && context.mounted) {
                  openPage(context, const ProductListPage(
                      title: 'Giỏ hàng', path: '/api/v1/cart'));
                }
              }),
          ]),
        body: IndexedStack(index: index, children: pages),
        bottomNavigationBar: NavigationBar(selectedIndex: index,
          onDestinationSelected: (i) async {
            if (i == 2) {
              if (await needAccount(context) && context.mounted) {
                openPage(context, const ListingPage());
              }
            } else {
              setState(() => index = i);
              fetchCounts();
            }
          }, destinations: [
            const NavigationDestination(icon: Icon(Icons.home_outlined),
                selectedIcon: Icon(Icons.home), label: 'Trang chủ'),
            const NavigationDestination(icon: Icon(Icons.search), label: 'Khám phá'),
            const NavigationDestination(icon: Icon(Icons.add_circle_outline, color: rose),
                label: 'Ký gửi'),
            NavigationDestination(icon: badge(Icons.notifications_none,
                number(counts['notifications']).toInt()), label: 'Thông báo'),
            const NavigationDestination(icon: Icon(Icons.person_outline), label: 'Tôi'),
          ]),
      );
    },
  );
}
