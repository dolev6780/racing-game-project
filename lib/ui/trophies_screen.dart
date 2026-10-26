import 'package:flutter/material.dart';

import '../data/achievements.dart';
import '../data/championship.dart';
import '../data/profile.dart';
import '../sim/track.dart';
import 'theme.dart';

/// Trophy cabinet (league cups), achievements with progress, career stats and best laps.
class TrophiesScreen extends StatelessWidget {
  const TrophiesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: MenuBackdrop(
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
            child: Row(children: [
              Flexible(child: FittedBox(fit: BoxFit.scaleDown, child: Text('TROPHY ROOM', style: title(44, AppColors.accent)))),
              const Spacer(),
              Text('${Achievements.unlockedCount} / ${Achievements.all.length} ACHIEVEMENTS', style: title(20)),
            ]),
          ),
          FittedBox(fit: BoxFit.scaleDown, child: _cabinet()),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: LayoutBuilder(builder: (context, c) {
                final wide = c.maxWidth > 900;
                final list = _achievementList();
                final stats = _stats();
                return wide
                    ? Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Expanded(flex: 3, child: list),
                        const SizedBox(width: 16),
                        Expanded(flex: 2, child: stats),
                      ])
                    : ListView(children: [SizedBox(height: 420, child: list), const SizedBox(height: 16), SizedBox(height: 520, child: stats)]);
              }),
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(left: 16, bottom: 12),
            child: Align(alignment: Alignment.bottomLeft, child: GameButton('BACK', width: 180, onPressed: () => Navigator.of(context).pop())),
          ),
        ]),
      ),
    );
  }

  Widget _cabinet() {
    return GamePanel(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 20),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        for (int i = 0; i < Championship.leagues.length; i++)
          Builder(builder: (_) {
            final best = Profile.champBest(i);
            final color = switch (best) { 1 => AppColors.gold, 2 => AppColors.silver, 3 => AppColors.bronze, _ => Colors.white12 };
            return SizedBox(
              width: 180,
              child: Column(children: [
                Icon(Icons.emoji_events, size: 64, color: color,
                    shadows: [if (best > 0 && best <= 3) Shadow(color: color, blurRadius: 18)]),
                Text(Championship.leagues[i].name, style: title(15, best > 0 ? Colors.white : AppColors.dim)),
                Text(best == 0 ? 'NOT WON YET' : best == 1 ? 'CHAMPION' : 'BEST: ${ordinal(best)}',
                    style: body(12, best == 1 ? AppColors.accent : AppColors.dim)),
              ]),
            );
          }),
      ]),
    );
  }

  Widget _achievementList() {
    final list = List.of(Achievements.all)
      ..sort((a, b) {
        double score(AchievementDef x) {
          if (Profile.achieved(x.id)) return 2;
          if (x.progress == null) return 0;
          final (cur, target) = x.progress!();
          return target > 0 ? cur / target : 0;
        }

        return score(b).compareTo(score(a));
      });
    return GamePanel(
      padding: const EdgeInsets.all(12),
      child: ListView(children: [
        for (final a in list) _achievementRow(a),
      ]),
    );
  }

  Widget _achievementRow(AchievementDef a) {
    final done = Profile.achieved(a.id);
    (int, int)? prog;
    if (!done && a.progress != null) prog = a.progress!();
    return Container(
      margin: const EdgeInsets.symmetric(vertical: 4),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: done ? AppColors.accent.withValues(alpha: 0.16) : AppColors.panelLight,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(children: [
        Icon(Icons.emoji_events, size: 36, color: done ? AppColors.gold : Colors.white24),
        const SizedBox(width: 10),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(a.name, style: title(17, done ? Colors.white : AppColors.dim)),
            Text(a.description, style: body(12, AppColors.dim)),
            if (prog != null && prog.$2 > 1)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Row(children: [
                  Expanded(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                          value: prog.$1 / prog.$2, minHeight: 7, color: AppColors.accent, backgroundColor: Colors.white12),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text('${prog.$1}/${prog.$2}', style: body(11, AppColors.dim)),
                ]),
              ),
          ]),
        ),
        const SizedBox(width: 10),
        Text(done ? 'DONE' : money(a.reward), style: title(16, done ? AppColors.good : AppColors.accent)),
      ]),
    );
  }

  Widget _stats() {
    final races = Profile.stat(Stat.races), wins = Profile.stat(Stat.wins);
    final rows = <(String, String)>[
      ('RACES', '$races'),
      ('WINS', '$wins  (${races > 0 ? wins * 100 ~/ races : 0}%)'),
      ('PODIUMS', '${Profile.stat(Stat.podiums)}'),
      ('BEST WIN STREAK', '${Profile.stat(Stat.bestWinStreak)}'),
      ('OPPONENTS WRECKED', '${Profile.stat(Stat.wrecks)}'),
      ('TIMES WRECKED', '${Profile.stat(Stat.timesWrecked)}'),
      ('MISSILES FIRED', '${Profile.stat(Stat.missilesFired)}'),
      ('MINE HITS', '${Profile.stat(Stat.mineHits)}'),
      ('NITROS USED', '${Profile.stat(Stat.nitrosUsed)}'),
      ('TOP SPEED', '${Profile.stat(Stat.topSpeedKmh)} KM/H'),
      ('MONEY EARNED', money(Profile.stat(Stat.moneyEarned))),
    ];
    return GamePanel(
      padding: const EdgeInsets.all(14),
      child: ListView(children: [
        Text('CAREER', style: title(24, AppColors.accent)),
        for (final (k, v) in rows) _statRow(k, v),
        const SizedBox(height: 12),
        Text('BEST LAPS', style: title(20, AppColors.accent)),
        for (final t in tracks)
          _statRow(t.name, Profile.bestLap(t.name) > 0 ? _fmt(Profile.bestLap(t.name)) : '--'),
      ]),
    );
  }

  static String _fmt(double t) => '${t ~/ 60}:${(t % 60).toStringAsFixed(2).padLeft(5, '0')}';

  Widget _statRow(String k, String v) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(children: [Expanded(child: Text(k, style: body(15, AppColors.dim))), Text(v, style: title(16))]),
      );
}
