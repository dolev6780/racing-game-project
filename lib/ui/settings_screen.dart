import 'package:flutter/material.dart';

import '../data/profile.dart';
import 'theme.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: MenuBackdrop(
        child: Center(
          child: SingleChildScrollView(
            child: SizedBox(
              width: 620,
              child: GamePanel(
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  Text('SETTINGS', style: title(44, AppColors.accent)),
                  const SizedBox(height: 14),
                  OptionRow(
                    label: 'TOUCH CONTROLS',
                    options: const ['OFF', 'ON'],
                    index: Profile.showTouch ? 1 : 0,
                    onChanged: (i) => setState(() => Profile.showTouch = i == 1),
                  ),
                  OptionRow(
                    label: 'STEERING',
                    options: const ['ARROWS', 'STICK'],
                    index: Profile.steering.index,
                    onChanged: (i) => setState(() => Profile.steering = SteeringMode.values[i]),
                  ),
                  OptionRow(
                    label: 'SHOW FPS',
                    options: const ['OFF', 'ON'],
                    index: Profile.showFps ? 1 : 0,
                    onChanged: (i) => setState(() => Profile.showFps = i == 1),
                  ),
                  OptionRow(
                    label: 'CAMERA ZOOM',
                    options: const ['CLOSE', 'WIDE'],
                    index: Profile.camera.index,
                    onChanged: (i) => setState(() => Profile.camera = CameraView.values[i]),
                  ),
                  OptionRow(
                    label: 'VOLUME',
                    options: [for (int v = 0; v <= 10; v++) '${v * 10}%'],
                    index: (Profile.volume * 10).round().clamp(0, 10),
                    onChanged: (i) => setState(() => Profile.volume = i / 10),
                  ),
                  const SizedBox(height: 18),
                  GameButton('RESET PROGRESS', width: 280, color: const Color(0xFF731F1F), onPressed: () async {
                    final ok = await showDialog<bool>(
                      context: context,
                      builder: (c) => AlertDialog(
                        title: const Text('Reset all progress?'),
                        content: const Text('Money, vehicles, upgrades, stats and championship progress will be deleted.'),
                        actions: [
                          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel')),
                          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Reset')),
                        ],
                      ),
                    );
                    if (ok == true) {
                      await Profile.resetAll();
                      setState(() {});
                    }
                  }),
                  const SizedBox(height: 12),
                  GameButton.primary('BACK', width: 240, onPressed: () => Navigator.of(context).pop()),
                ]),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
