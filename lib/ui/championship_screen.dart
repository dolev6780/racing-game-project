import 'package:flutter/material.dart';

import '../data/championship.dart';
import '../data/profile.dart';
import '../sim/track.dart';
import 'garage_screen.dart';
import 'race_screen.dart';
import 'theme.dart';

/// League select -> season hub (calendar, standings, next race) -> season finale.
class ChampionshipScreen extends StatefulWidget {
  const ChampionshipScreen({super.key});

  @override
  State<ChampionshipScreen> createState() => _ChampionshipScreenState();
}

class _ChampionshipScreenState extends State<ChampionshipScreen> {
  bool _showLeagues = false;

  @override
  Widget build(BuildContext context) {
    Widget page;
    if (Championship.seasonComplete) {
      page = _finale();
    } else if (Championship.hasActiveSeason && !_showLeagues) {
      page = _hub();
    } else {
      page = _leagues();
    }
    return Scaffold(
      body: MenuBackdrop(
        child: Stack(children: [
          Positioned(right: 24, top: 16, child: BankChip(Profile.money)),
          Positioned.fill(child: page),
        ]),
      ),
    );
  }

  Widget _header(String text) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
        child: Align(alignment: Alignment.centerLeft, child: Text(text, style: title(46, AppColors.accent))),
      );

  // ---------- Leagues ----------

  Widget _leagues() {
    return Column(children: [
      _header('CHAMPIONSHIP'),
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24),
        child: Align(
          alignment: Alignment.centerLeft,
          child: Text('Win a league to unlock the next one. Upgrade your car in the garage between races!',
              style: body(16, AppColors.dim)),
        ),
      ),
      Expanded(
        child: Center(
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.all(16),
            child: Row(children: [for (int i = 0; i < Championship.leagues.length; i++) _leagueCard(i)]),
          ),
        ),
      ),
      Padding(
        padding: const EdgeInsets.all(16),
        child: Align(
          alignment: Alignment.bottomLeft,
          child: GameButton('BACK', width: 180, onPressed: () {
            if (_showLeagues) {
              setState(() => _showLeagues = false);
            } else {
              Navigator.of(context).pop();
            }
          }),
        ),
      ),
    ]);
  }

  Widget _leagueCard(int i) {
    final l = Championship.leagues[i];
    final locked = i > Championship.unlocked;
    final won = Profile.champWon(i);
    final active = Championship.activeLeague == i;
    return Container(
      width: 260,
      margin: const EdgeInsets.symmetric(horizontal: 8),
      child: GamePanel(
        color: locked ? const Color(0xB3080810) : AppColors.panel,
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Container(height: 8, decoration: BoxDecoration(
              color: locked ? Colors.white12 : Color.lerp(AppColors.accent, AppColors.hot, i / 3), borderRadius: BorderRadius.circular(4))),
          const SizedBox(height: 10),
          Text(l.name, textAlign: TextAlign.center, style: title(24, locked ? AppColors.dim : Colors.white)),
          Text('${l.tracks.length} RACES  |  ${l.laps} LAPS', style: body(13, AppColors.dim)),
          const SizedBox(height: 10),
          for (final t in l.tracks) Text(tracks[t].name, style: body(14, locked ? AppColors.dim : Colors.white)),
          const SizedBox(height: 10),
          Text('PRIZES x${l.prizeMultiplier}', style: title(18, AppColors.accent)),
          Text('WIN BONUS ${money(l.winBonus)}', style: body(14)),
          if (won) Padding(padding: const EdgeInsets.only(top: 6), child: Text('WON', style: title(20, AppColors.good))),
          const SizedBox(height: 14),
          if (locked)
            Text('LOCKED\nwin the previous league', textAlign: TextAlign.center, style: body(14, AppColors.dim))
          else if (active)
            GameButton.primary('CONTINUE', width: 200, onPressed: () => setState(() => _showLeagues = false))
          else
            GameButton.primary(won ? 'RACE AGAIN' : 'ENTER', width: 200,
                onPressed: Championship.hasActiveSeason
                    ? null
                    : () => setState(() {
                          Championship.startSeason(i);
                          _showLeagues = false;
                        })),
        ]),
      ),
    );
  }

  // ---------- Season hub ----------

  Widget _hub() {
    final l = Championship.active!;
    final next = Championship.nextRace;
    final calendar = GamePanel(
      child: SizedBox(
        width: 420,
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text('CALENDAR', style: title(24, AppColors.accent)),
          const SizedBox(height: 8),
          for (int i = 0; i < l.tracks.length; i++)
            Container(
              margin: const EdgeInsets.symmetric(vertical: 4),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: i == next ? AppColors.accent.withValues(alpha: 0.2) : AppColors.panelLight,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(children: [
                Container(width: 36, height: 36, decoration: BoxDecoration(
                    color: tracks[l.tracks[i]].theme.ground, borderRadius: BorderRadius.circular(8))),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(tracks[l.tracks[i]].name, style: title(17)),
                    Text(tracks[l.tracks[i]].subtitle, style: body(12, AppColors.dim)),
                  ]),
                ),
                if (i < next)
                  Text('${ordinal(Profile.champResult(i))}  +${Profile.champResultPoints(i)}', style: title(16, AppColors.accent))
                else if (i == next)
                  Text('NEXT', style: title(16, AppColors.accent)),
              ]),
            ),
        ]),
      ),
    );
    return Column(children: [
      _header(l.name),
      Text('RACE ${next + 1} OF ${l.tracks.length}', style: title(20)),
      Expanded(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(12),
            child: Wrap(spacing: 20, runSpacing: 20, alignment: WrapAlignment.center, children: [
              calendar,
              _standingsPanel('STANDINGS'),
            ]),
          ),
        ),
      ),
      Padding(
        padding: const EdgeInsets.all(16),
        child: Wrap(spacing: 14, runSpacing: 10, alignment: WrapAlignment.center, children: [
          GameButton('BACK', width: 150, onPressed: () => Navigator.of(context).pop()),
          GameButton('ABANDON', width: 170, color: const Color(0xFF731F1F), onPressed: () => setState(Championship.abandon)),
          GameButton('GARAGE', width: 170, onPressed: () async {
            await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const GarageScreen()));
            setState(() {});
          }),
          GameButton.primary('NEXT RACE', width: 260, onPressed: _startNext),
        ]),
      ),
    ]);
  }

  Future<void> _startNext() async {
    final setup = Championship.nextRaceSetup();
    if (setup == null) return;
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => RaceScreen(setup: setup)));
    if (mounted) setState(() {});
  }

  Widget _standingsPanel(String heading) {
    final s = Championship.standings();
    return GamePanel(
      child: SizedBox(
        width: 360,
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(heading, style: title(24, AppColors.accent)),
          const SizedBox(height: 8),
          for (int i = 0; i < s.length; i++)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: s[i].isPlayer ? AppColors.accent.withValues(alpha: 0.2) : Colors.transparent,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(children: [
                Expanded(child: Text('${i + 1}.  ${s[i].name.toUpperCase()}', style: body(17, s[i].isPlayer ? AppColors.accent : Colors.white))),
                Text('${s[i].points} PTS', style: title(17, s[i].isPlayer ? AppColors.accent : Colors.white)),
              ]),
            ),
        ]),
      ),
    );
  }

  // ---------- Finale ----------

  Widget _finale() {
    final league = Championship.activeLeague;
    final l = Championship.active!;
    final position = Championship.playerSeasonPosition();
    final bonus = Championship.seasonBonus(league, position);
    final willUnlock = position == 1 && league + 1 < Championship.leagues.length && Championship.unlocked < league + 1;
    return Column(children: [
      _header('${l.name} - FINAL'),
      Expanded(
        child: Center(
          child: SingleChildScrollView(
            child: Wrap(spacing: 20, runSpacing: 20, alignment: WrapAlignment.center, children: [
              _standingsPanel('FINAL STANDINGS'),
              GamePanel(
                child: SizedBox(
                  width: 420,
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                    Text(position == 1 ? 'CHAMPION!' : '${ordinal(position)} OVERALL',
                        style: title(52, position <= 3 ? AppColors.accent : Colors.white)),
                    const SizedBox(height: 16),
                    Text(bonus > 0 ? 'SEASON BONUS  ${money(bonus)}' : 'NO BONUS THIS TIME', style: title(22)),
                    const SizedBox(height: 10),
                    Text(
                      willUnlock
                          ? '${Championship.leagues[league + 1].name} UNLOCKED!'
                          : position == 1
                              ? 'LEAGUE CONQUERED'
                              : 'Win the league to unlock the next one.',
                      style: body(17, willUnlock ? AppColors.good : AppColors.dim),
                    ),
                  ]),
                ),
              ),
            ]),
          ),
        ),
      ),
      Padding(
        padding: const EdgeInsets.all(16),
        child: GameButton.primary('COLLECT', width: 280, onPressed: () => setState(Championship.finishSeason)),
      ),
    ]);
  }
}
