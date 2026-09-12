using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VocaNerd.EditorTools
{
    /// <summary>
    /// SpriteAnimation の sprites 配列を 1 枚ずつ刺さなくて済むようにする Inspector 拡張。
    ///
    ///   - ドロップ枠に Sprite / Texture / フォルダ を放り込むと、中の Sprite を全部入れる
    ///   - 「選択中の Sprite を設定」ボタンは Project ビューの選択から入れる
    ///     (Inspector をロックしてから選択する使い方)
    ///
    /// 並びは既定で名前の自然順 (small-ken2 < small-ken10)。
    /// </summary>
    [CustomEditor(typeof(SpriteAnimation))]
    [CanEditMultipleObjects]
    public class SpriteAnimationEditor : Editor
    {
        private const string SpritesPropertyName = "sprites";

        private bool _sortByName = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("一括設定", EditorStyles.boldLabel);

            _sortByName = EditorGUILayout.ToggleLeft("名前の自然順に並べ替える", _sortByName);

            DrawDropArea();

            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = CollectSprites(Selection.objects);
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button(selected.Count > 0
                            ? $"選択中の Sprite を設定 ({selected.Count})"
                            : "選択中の Sprite を設定"))
                    {
                        ApplySprites(selected);
                    }
                }

                if (GUILayout.Button("クリア", GUILayout.Width(80f)))
                    ApplySprites(new List<Sprite>());
            }

            EditorGUILayout.HelpBox(
                "Project ビューで Sprite を選ぶと Inspector の表示が切り替わってしまうので、" +
                "ボタンを使う場合は先に Inspector 右上の鍵アイコンでロックしてください。" +
                "ドロップ枠を使えばロックは不要です。",
                MessageType.None);
        }

        private void DrawDropArea()
        {
            var rect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "ここに Sprite / Texture / フォルダ をドロップ", EditorStyles.helpBox);

            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;
            if (!rect.Contains(evt.mousePosition))
                return;

            var dragged = CollectSprites(DragAndDrop.objectReferences);
            DragAndDrop.visualMode = dragged.Count > 0
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform && dragged.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                ApplySprites(dragged);
            }
            evt.Use();
        }

        /// <summary>
        /// Sprite そのもの / Texture (複数 Sprite に分割されたシート) / フォルダ のどれでも
        /// 中の Sprite を集める。重複は除く。
        /// </summary>
        private List<Sprite> CollectSprites(IEnumerable<Object> objects)
        {
            var result = new List<Sprite>();
            if (objects == null) return result;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                if (obj is Sprite sprite)
                {
                    result.Add(sprite);
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { path }))
                    {
                        var childPath = AssetDatabase.GUIDToAssetPath(guid);
                        result.AddRange(LoadSpritesAt(childPath));
                    }
                    continue;
                }

                if (obj is Texture2D)
                    result.AddRange(LoadSpritesAt(path));
            }

            result = result.Where(s => s != null).Distinct().ToList();
            if (_sortByName)
                result.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return result;
        }

        // 1 枚のテクスチャが Multiple で複数 Sprite に切られている場合も全部拾う
        private static IEnumerable<Sprite> LoadSpritesAt(string path)
            => AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Sprite>()
                .Concat(new[] { AssetDatabase.LoadAssetAtPath<Sprite>(path) })
                .Where(s => s != null);

        private void ApplySprites(IReadOnlyList<Sprite> sprites)
        {
            serializedObject.Update();

            var prop = serializedObject.FindProperty(SpritesPropertyName);
            if (prop == null)
            {
                Debug.LogError($"[SpriteAnimationEditor] '{SpritesPropertyName}' が見つかりません");
                return;
            }

            prop.arraySize = sprites.Count;
            for (var i = 0; i < sprites.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];

            serializedObject.ApplyModifiedProperties();

            if (sprites.Count > 0)
                Debug.Log($"[SpriteAnimationEditor] {sprites.Count} 枚を設定しました " +
                          $"({sprites[0].name} 〜 {sprites[sprites.Count - 1].name})");
        }
    }
}
