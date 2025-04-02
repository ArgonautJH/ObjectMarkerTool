using System;
using System.Collections.Generic;
using ArgonautJH.ObjectMarkerTool.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Rendering;
using System.IO;

namespace ArgonautJH.ObjectMarkerTool.Editor
{
    /// <summary>
    /// 오브젝트 생성기 윈도우
    ///
    /// todo 추후 메테리얼 생성 여부 선택 가능하도록 할지 고민 필요
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
        
        // 캐싱용 딕셔너리 (프리셋 이름을 키로 사용)
        private Dictionary<string, Material> _materialCache = new();
        
        private Dictionary<string, IMaterialConfigurator> shaderConfigurators = new()
        {
            { "HDRP/Lit", new HDRPLitConfigurator() },
            { "Universal Render Pipeline/Lit", new URPLitConfigurator() },
            { "Standard", new StandardConfigurator() }
        };
        
        // 스크립트 추출 경로 (사용자 지정)
        private string _exportScriptFolder = "";
        
        [MenuItem("Tools/Custom Object Creator")]
        public static void ShowWindow()
        {
            GetWindow<ObjectMarkerTool>("오브젝트 생성기");
        }

        private void OnGUI()
        {
            GUILayout.Label("추출 경로 설정", EditorStyles.boldLabel);
            // 스크립트 추출 경로 설정 UI
            if (GUILayout.Button("스크립트 추출 경로 선택"))
            {
                _exportScriptFolder = EditorUtility.OpenFolderPanel("스크립트 추출 경로 선택", Application.dataPath, "");

                // 스크립트 추출 경로를 결정 (사용자가 선택하지 않으면 기본 경로 사용)
                if (string.IsNullOrEmpty(_exportScriptFolder))
                {
                    _exportScriptFolder = "Assets/TempScript";
                }
            }
            EditorGUILayout.LabelField("스크립트 추출 경로:", _exportScriptFolder);

            if (GUILayout.Button("스크립트 추출하기"))
            {
                if (_exportScriptFolder.Equals("Assets/TempScript"))
                {
                    if (!AssetDatabase.IsValidFolder(_exportScriptFolder))
                    {
                        AssetDatabase.CreateFolder("Assets", "TempScript");
                    }
                }
                
                EnsureObjectComponentInExportFolder(_exportScriptFolder);
            }
            
            // "일괄 컴포넌트 업데이트" 버튼: 씬 내 기존 오브젝트의 ObjectComponent를 ExportedObjectComponent로 변환
            if (GUILayout.Button("일괄 컴포넌트 업데이트"))
            {
                UpdateExistingComponentsInScene();
            }
            
            
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
        
        /// <summary>
        /// 사용자가 입력한 정보를 바탕으로 오브젝트 생성
        /// </summary>
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
                // spawnPosition = SceneView.lastActiveSceneView.pivot;
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                float distance = 3f; // 카메라에서 5 유닛 앞에 생성
                spawnPosition = sceneCam.transform.position + sceneCam.transform.forward * distance;
            }

            GameObject newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newObj.name = "CustomObject";
            newObj.transform.position = spawnPosition;

            // Renderer가 있으면 프리셋의 색상과 투명도를 적용한 Material 생성 및 할당
            Renderer renderer = newObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = GetOrCreateMaterial(selectedPreset);
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
            
            // 사용자가 스크립트 추출 경로를 지정했다면 해당 위치에 ObjectComponent가 있는지 확인/복사
            bool useExportedComponent = EnsureObjectComponentInExportFolder(_exportScriptFolder);
            
            if (useExportedComponent)
            {
                Type exportedType = GetExportedObjectComponentType();
                if (exportedType != null)
                {
                    var comp = newObj.AddComponent(exportedType);
                    var summaryField = exportedType.GetField("summary");
                    var textField = exportedType.GetField("text");
                    if (summaryField != null) summaryField.SetValue(comp, _summary);
                    if (textField != null) textField.SetValue(comp, _text);
                }
                else
                {
                    Debug.LogError("ExportedObjectComponent 타입을 찾을 수 없습니다.");
                }
            }
            else
            {
                // 만약 복사에 실패하면 기본 ObjectComponent 사용
                ObjectComponent comp = newObj.AddComponent<ObjectComponent>();
                comp.summary = _summary;
                comp.text = _text;
            }
            
            // // ObjectComponent 컴포넌트
            // ObjectComponent comp = newObj.GetComponent<ObjectComponent>();
            // if (comp == null)
            // {
            //     comp = newObj.AddComponent<ObjectComponent>();
            // }
            // comp.summary = _summary;
            // comp.text = _text;
            
            // 새로 생성된 오브젝트 선택
            Selection.activeGameObject = newObj;
            
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        
        /// <summary>
        /// 지정된 폴더에 ExportedObjectComponent 스크립트가 없으면
        /// 원본 ObjectComponent.cs를 복사하고 클래스 이름을 치환하여 생성
        /// </summary>
        /// <param name="folderPath">대상 폴더 경로 (예: Assets/TempScript 또는 사용자가 선택한 경로)</param>
        /// <returns>복사에 성공하면 true</returns>
        private bool EnsureObjectComponentInExportFolder(string folderPath)
        {
            // 대상 경로: {folderPath}/ExportedObjectComponent.cs
            string targetFile = Path.Combine(folderPath, "ExportedObjectComponent.cs");
            
            // 테스트 모드일 경우와 아닐 경우의 경로를 분기합니다.
#if TEST_MODE
            // 예: 테스트 시 로컬 경로 (Assets 내의 임시 스크립트 폴더)
            string sourcePath = "Assets/ObjectMarkerTool/Runtime/Scripts/ObjectComponent.cs";
#else
        // UMP 패키지 경로
        string sourcePath = "Packages/com.argonautjh.objectmarkertool/Runtime/Scripts/ObjectComponent.cs";
#endif

            if (!File.Exists(targetFile))
            {
                // // 원본 경로 (실제 UPM 패키지 내 경로에 맞게 수정)
                // string sourcePath = "Packages/com.argonautjh.objectmarkertool/Runtime/Scripts/ObjectComponent.cs";
                if (File.Exists(sourcePath))
                {
                    try
                    {
                        // 원본 코드 읽기
                        string sourceCode = File.ReadAllText(sourcePath);
                        // 클래스 이름 변경: "public class ObjectComponent" -> "public class ExportedObjectComponent"
                        string newCode = sourceCode.Replace("public class ObjectComponent", "public class ExportedObjectComponent");
                        // 변경된 코드를 대상 파일에 저장
                        File.WriteAllText(targetFile, newCode);

                        // meta 파일도 복사 (GUID 유지)
                        string sourceMeta = sourcePath + ".meta";
                        string targetMeta = targetFile + ".meta";
                        if (File.Exists(sourceMeta))
                        {
                            File.Copy(sourceMeta, targetMeta, true);
                        }
                        AssetDatabase.ImportAsset(targetFile);
                        Debug.Log("ExportedObjectComponent 스크립트가 생성되었습니다: " + targetFile);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("스크립트 복사/치환 중 오류 발생: " + ex.Message);
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("원본 ObjectComponent 스크립트를 찾을 수 없습니다: " + sourcePath);
                    return false;
                }
            }
            else
            {
                Debug.Log("ExportedObjectComponent 스크립트가 이미 존재합니다: " + targetFile);
                return true;
            }
        }
        
        /// <summary>
        /// 씬 내의 기존 ObjectComponent를 ExportedObjectComponent로 변환
        /// </summary>
        private void UpdateExistingComponentsInScene()
        {
            // ExportedObjectComponent 타입 검색
            Type exportedType = GetExportedObjectComponentType();
            if (exportedType == null)
            {
                Debug.LogError("ExportedObjectComponent 타입을 찾을 수 없습니다. 스크립트를 내보내세요.");
                return;
            }

            // 씬 내의 모든 ObjectComponent를 찾음
            ObjectComponent[] oldComponents = FindObjectsOfType<ObjectComponent>(true);
            int updateCount = 0;

            foreach (ObjectComponent oldComp in oldComponents)
            {
                GameObject go = oldComp.gameObject;
                // 기존 데이터를 저장
                string summaryData = oldComp.summary;
                string textData = oldComp.text;

                // 기존 컴포넌트 제거
                DestroyImmediate(oldComp, true);

                // 새 ExportedObjectComponent 추가
                var newComp = go.AddComponent(exportedType);
                var summaryField = exportedType.GetField("summary");
                var textField = exportedType.GetField("text");
                if (summaryField != null)
                    summaryField.SetValue(newComp, summaryData);
                if (textField != null)
                    textField.SetValue(newComp, textData);

                updateCount++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("업데이트 완료", updateCount + "개의 컴포넌트가 업데이트되었습니다.", "확인");
        }
        
        /// <summary>
        /// AppDomain 내에서 ExportedObjectComponent 타입을 검색
        /// </summary>
        private Type GetExportedObjectComponentType()
        {
            string typeName = "ArgonautJH.ObjectMarkerTool.Runtime.ExportedObjectComponent";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(typeName);
                if (t != null)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// 새 Material을 생성하거나 존재한다면 캐시된 Material을 반환
        /// </summary>
        private Material GetOrCreateMaterial(MaterialPreset preset)
        {
            // 이미 생성된 Material이 있다면 재사용
            if (_materialCache.TryGetValue(preset.PresetName, out Material cachedMat))
            {
                return cachedMat;
            }
            
            // Material 생성
            string shaderName = GetDefaultShaderName();
            Material newMat = new Material(Shader.Find(shaderName));

            // 정확히 동일한 키가 존재한다면 매핑
            if (shaderConfigurators.TryGetValue(shaderName, out IMaterialConfigurator configurator))
            {
                configurator.Configure(newMat, preset.Color, preset.Alpha);
            }
            else
            {
                // 매핑 실패 시 기본 동작
                Debug.LogWarning($"No configurator found for shader: {shaderName}");
            }
            
            // _presetDatabase 에셋이 있는 위치를 기준으로 CustomMaterials 폴더 생성
            if (_presetDatabase != null)
            {
                string presetAssetPath = AssetDatabase.GetAssetPath(_presetDatabase);
                string presetFolderPath = Path.GetDirectoryName(presetAssetPath);
                string customMaterialsFolder = Path.Combine(presetFolderPath, _presetDatabase.name+"_Materials");
        
                if (!AssetDatabase.IsValidFolder(customMaterialsFolder))
                {
                    AssetDatabase.CreateFolder(presetFolderPath, _presetDatabase.name+"_Materials");
                }
        
                // 프리셋 이름을 파일 이름으로 사용하여 에셋 저장
                string assetPath = Path.Combine(customMaterialsFolder, $"{preset.PresetName}.mat");
                AssetDatabase.CreateAsset(newMat, assetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                // _presetDatabase가 없는 경우 기본 경로 사용
                string folderPath = "Assets/CustomMaterials";
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder("Assets", "CustomMaterials");
                }
                string assetPath = $"{folderPath}/{preset.PresetName}.mat";
                AssetDatabase.CreateAsset(newMat, assetPath);
                AssetDatabase.SaveAssets();
            }
    
            // 캐시에 저장 후 반환
            _materialCache[preset.PresetName] = newMat;
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

