using UnityEditor;
using UnityEngine;

namespace ECSReact.Editor.CodeGeneration
{
  public class CodePreviewWindow : EditorWindow
  {
    private string code;
    private Vector2 scrollPosition;

    public void SetCode(string code)
    {
      this.code = code;
    }

    private void OnGUI()
    {
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      var style = new GUIStyle(EditorStyles.textArea);
      style.wordWrap = false;

      EditorGUILayout.TextArea(code, style, GUILayout.ExpandHeight(true));

      EditorGUILayout.EndScrollView();
    }
  }
}