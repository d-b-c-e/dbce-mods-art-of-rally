"""Read-only source checks; these do not execute C# or establish rendered coverage."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[3]
plan = (root / "tools/testing/Recorder/UiFinalCapturePlan.cs").read_text()
capture = (root / "tools/testing/Recorder/UiCapture.cs").read_text()
panel = (root / "src/ArtOfSimRally.Mod/SettingsPanel.cs").read_text()
keys = (root / "src/ArtOfSimRally.Mod/CameraKeys.cs").read_text()
checks = 0


def check(ok, reason):
    global checks
    checks += 1
    if not ok:
        raise AssertionError(reason)


count = len(re.findall(r'new Binding\("', keys))
check(count == 11 and f"pair < {count}" in plan, "camera table/queue length drift")
check('"camkey-bind-" + (pair + 1)' in plan and '"cmd-Restore numpad defaults"' in plan,
      "next-row or terminal anchor missing")
check('ArtButtonBinding(c, WheelInput.CameraChannel(index), binding.Label + " (USB button)")' in panel,
      "camera keyboard/USB pairing changed")
check('Disclosure(_modButtons, "mod buttons and Settings key")' in panel
      and '"disclosure-mod buttons and Settings key"' in plan, "Look forward following anchor missing")
check('RightButton("Restore numpad defaults", 180)' in panel and 'W.Command("cmd-" + label' in panel,
      "terminal button ID changed")
check('"final-help-top", "Simple", 4, 0' in plan and '"final-help-lower", "Simple", 4, 100000' in plan,
      "Help top/lower navigation missing")
block = capture.split('foreach (var capture in UiFinalCapturePlan.Create())', 1)[1].split('else if (followup)', 1)[0]
check(all(token not in block for token in ('HostCall', 'Save(', 'BeginCapture', 'OpenURL', 'SendInput')),
      "render navigation gained an action")
check('var target = capture;' in block and 'Add(target.Name' in block and 'Focus(target.Focus)' in block,
      "production queue no longer consumes the plan through existing steps")
names = ['open', 'final-controls-top', 'final-actions-top', 'final-actions-lower', 'final-cameras-top']
names += [f'final-camera-binding-{i:02}' for i in range(count - 1)]
names += ['final-cameras-lower', 'final-advanced-cameras-top', 'final-help-top', 'final-help-lower',
          'bindings-state', 'bindings-saved', 'bindings-saved-row', 'saved-closed']
expected = json.loads((root / 'tools/testing/Recorder/fixtures/final-ui-expected-shots.json').read_text())
check(expected == [f'{i:02}-{name}.png' for i, name in enumerate(names, 1)], 'ExpectedShots order differs')
check(len(expected) == 23 and len(set(expected)) == 23, 'shot count or duplicate names')
print(json.dumps(dict(status='passed', kind='static-source-only', checks=checks,
                      expectedShots=len(expected), csharpFixtureExecuted=False, renderedCoverage=False)))
