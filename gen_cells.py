#!/usr/bin/env python3
import os
os.chdir("C:/01_Project/03_Crux/CRUX/Assets/_Project/Prefabs/UI/BattleHUD")

cells = [
    {'cellGO': 8583291650520394164, 'cellRect': 1557772914284303335, 'hlayout': 4756745327897495005, 'labelGO': 2974086228520793100, 'labelRect': 3289808246358303394, 'labelText': 4876534537742470541, 'labelLayout': 7654321098765432101, 'dotGO': 9611352386077021823, 'dotRect': 2210952854604922013, 'dotImage': 9633104508767029194, 'dotLayout': 8765432109876543210, 'name': 'Track', 'label': '\uad6c\ub3d9', 'r': 0.922, 'g': 0.427, 'b': 0.322},
    {'cellGO': 2665932331202020734, 'cellRect': 5849905722344183116, 'hlayout': 4145251123281432549, 'labelGO': 1184760459714210736, 'labelRect': 1125748262497632681, 'labelText': 1246951066428002813, 'labelLayout': 9876543210987654321, 'dotGO': 6779305185166471720, 'dotRect': 6260060474028346804, 'dotImage': 4260470659986854295, 'dotLayout': 1111111111111111111, 'name': 'Gun', 'label': '\uc8fc\ud3ec', 'r': 0.957, 'g': 0.839, 'b': 0.286},
    {'cellGO': 5555555555555555555, 'cellRect': 6666666666666666666, 'hlayout': 7777777777777777777, 'labelGO': 8888888888888888888, 'labelRect': 9999999999999999999, 'labelText': 1234567890123456789, 'labelLayout': 2222222222222222222, 'dotGO': 3333333333333333333, 'dotRect': 4444444444444444444, 'dotImage': 5555555555555555556, 'dotLayout': 6666666666666666667, 'name': 'Turret', 'label': '\ud3ec\ud0d0', 'r': 0.286, 'g': 0.745, 'b': 0.533},
]

lines = []
for cell in cells:
    for key in ['hlayout', 'labelGO', 'labelRect', 'labelText', 'labelLayout', 'dotGO', 'dotRect', 'dotImage', 'dotLayout']:
        if key == 'hlayout':
            lines.append(f"--- !u!114 &{cell[key]}")
            lines.append("MonoBehaviour:")
            lines.append("  m_ObjectHideFlags: 0")
            lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
            lines.append("  m_PrefabInstance: {fileID: 0}")
            lines.append("  m_PrefabAsset: {fileID: 0}")
            lines.append(f"  m_GameObject: {{fileID: {cell['cellGO']}}}")
            lines.append("  m_Enabled: 1")
            lines.append("  m_EditorHideFlags: 0")
            lines.append("  m_Script: {fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}")
            lines.append("  m_Name: ")
            lines.append("  m_EditorClassIdentifier: ")
            lines.append("  m_Padding:")
            lines.append("    m_Left: 0")
            lines.append("    m_Right: 0")
            lines.append("    m_Top: 0")
            lines.append("    m_Bottom: 0")
            lines.append("  m_ChildForceExpandWidth: 1")
            lines.append("  m_ChildForceExpandHeight: 0")
            lines.append("  m_ChildControlSize: 1")
            lines.append("  m_ChildScaleWidth: 0")
            lines.append("  m_ChildScaleHeight: 0")
            lines.append("  m_Spacing: 0")
            lines.append("  m_ReverseArrangement: 0")

with open('UnitInfoCard.prefab', 'a', encoding='utf-8') as f:
    f.write('\n'.join(lines))
print(f"Added {len(lines)} lines")
