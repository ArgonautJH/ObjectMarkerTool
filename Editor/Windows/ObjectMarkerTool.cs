using System.Collections.Generic;
using ArgonautJH.ObjectMarkerTool.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Rendering;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    /// <summary>
    /// 오브젝트 생성기 윈도우
    /// </summary>
    public class ObjectMarkerTool : EditorWindow
    {
        private string _3dText = "";
        private float _3dFontSize = 1.0f;
        private string _summary = "";               // 제목
        private string _text = "";                  // 본문
        
        // 프리셋 데이터베이스를 에디터에서 참조할 수 있도록 필드로 선언
        private MaterialPresetDatabase _presetDatabase;
        private int _selectedPresetIndex = 0;
        
        private Dictionary<string, IMaterialConfigurator> shaderConfigurators = new()
        {
            { "HDRP/Lit", new HDRPLitConfigurator() },
            { "Universal Render Pipeline/Lit", new URPLitConfigurator() },
            { "Standard", new StandardConfigurator() }
        };
        
        [MenuItem("Tools/Custom Object Creator")]
        public static void ShowWindow()
        {
            GetWindow<ObjectMarkerTool>("오브젝트 생성기");
        }

        private void OnGUI()
        {
            
            GUILayout.Label("프리셋 설정", EditorStyles.boldLabel);
            // 프리셋 데이터베이스 에셋 참조
            _presetDatabase = (MaterialPresetDatabase)EditorGUILayout.ObjectField("프리셋 데이터베이스", _presetDatabase, typeof(MaterialPresetDatabase), false);
            
            if (_presetDatabase != null && _presetDatabase.Presets.Length > 0)
            {
                // 프리셋 이름을 배열로 추출해서 드롭다운 메뉴 생성
                string[] presetNames = new string[_presetDatabase.Presets.Length];
                for (int i = 0; i < _presetDatabase.Presets.Length; i++)
                {
                    presetNames[i] = _presetDatabase.Presets[i].PresetName;
                }
                _selectedPresetIndex = EditorGUILayout.Popup("프리셋 선택", _selectedPresetIndex, presetNames);
                
                // summary와 text 입력 필드
                _3dText = EditorGUILayout.TextField("큐브 글자", _3dText);
                _3dFontSize = EditorGUILayout.FloatField("큐브 글자 사이즈", _3dFontSize);
                _summary = EditorGUILayout.TextField("Summary", _summary);
                _text = EditorGUILayout.TextField("Text", _text);
            }
            else
            {
                EditorGUILayout.HelpBox("프리셋 데이터베이스가 없거나 비어있습니다.", MessageType.Warning);
            }

            if (GUILayout.Button("오브젝트 생성"))
            {
                CreateCustomObject();
            }
        }
        
        private void CreateCustomObject()
        {
            if (_presetDatabase == null || _presetDatabase.Presets.Length == 0)
            {
                Debug.LogError("프리셋 데이터베이스가 설정되지 않았거나, 프리셋이 없습니다!");
                return;
            }

            MaterialPreset selectedPreset = _presetDatabase.Presets[_selectedPresetIndex];

            // SceneView의 중앙 좌표에 오브젝트 생성
            Vector3 spawnPosition = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                spawnPosition = SceneView.lastActiveSceneView.pivot;
            }

            GameObject newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newObj.name = "CustomObject";
            newObj.transform.position = spawnPosition;

            // Renderer가 있으면 프리셋의 색상과 투명도를 적용한 Material 생성 및 할당
            Renderer renderer = newObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = ChangeMaterial(selectedPreset.Color, selectedPreset.Alpha);
            }

            // 글자를 표시할 자식 오브젝트 생성
            GameObject textObj = new GameObject("3DText");
            // 큐브 오브젝트의 자식으로 설정
            textObj.transform.SetParent(newObj.transform);
            // 텍스트가 큐브 중심에서 약간 앞으로 배치되도록
            textObj.transform.localPosition = new Vector3(0, 0, 0.6f);
            
            // TextMeshPro 컴포넌트 추가
            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = _3dText;                             // 입력받은 _3dText 표시
            tmp.fontSize = _3dFontSize;                            
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;                      // 글자 색상 (필요시 변경)
            
            // 글자를 3D 공간에서 회전/크기 조절 가능
            textObj.transform.localEulerAngles = new Vector3(0, 180, 0); 
            
            //  ColorChanger 컴포넌트
            ObjectComponent comp = newObj.GetComponent<ObjectComponent>();
            if (comp == null)
            {
                comp = newObj.AddComponent<ObjectComponent>();
            }
            comp.summary = _summary;
            comp.text = _text;
            
            // 새로 생성된 오브젝트 선택
            Selection.activeGameObject = newObj;
            
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// 새 Material을 생성하여 색상과 투명도 설정
        /// </summary>
        private Material ChangeMaterial(Color newColor, float alpha)
        {
            string shaderName = GetDefaultShaderName();
            // Debug.Log(shaderName);

            // Material 생성
            Material newMat = new Material(Shader.Find(shaderName));

            // 정확히 동일한 키가 존재한다면 매핑
            if (shaderConfigurators.TryGetValue(shaderName, out IMaterialConfigurator configurator))
            {
                configurator.Configure(newMat, newColor, alpha);
            }
            else
            {
                // 매핑 실패 시 기본 동작
                Debug.LogWarning($"No configurator found for shader: {shaderName}");
            }

            return newMat;
        }
        
        /// <summary>
        /// 현재 활성화된 렌더 파이프라인에 따른 기본 셰이더 이름을 반환
        /// </summary>
        private string GetDefaultShaderName()
        {
            var pipelineAsset = GraphicsSettings.renderPipelineAsset;
            if (pipelineAsset == null)
            {
                // 빌트인 렌더 파이프라인인 경우
                return "Standard";
            }
            else
            {
                string pipelineTypeName = pipelineAsset.GetType().Name;
                if (pipelineTypeName.Contains("Universal"))
                {
                    return "Universal Render Pipeline/Lit";
                }
                else if (pipelineTypeName.Contains("HD"))
                {
                    return "HDRP/Lit";
                }
                else
                {
                    // 예외 상황 처리
                    return "Standard";
                }
            }
        }
    }
}

