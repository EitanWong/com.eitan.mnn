using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>
    /// MNN编辑器本地化系统
    /// 自动同步Unity编辑器语言
    /// </summary>
    public static class MNNLocalization
    {
        private static SystemLanguage _currentLanguage;
        private static Dictionary<string, Dictionary<SystemLanguage, string>> _translations;
        private static bool _initialized;

        /// <summary>
        /// 当前语言
        /// </summary>
        public static SystemLanguage CurrentLanguage
        {
            get
            {
                if (!_initialized)
                    Initialize();
                return _currentLanguage;
            }
        }

        /// <summary>
        /// 初始化本地化系统
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (_initialized) return;

            // 自动检测Unity编辑器语言
            _currentLanguage = DetectEditorLanguage();

            // 加载翻译字典
            LoadTranslations();

            _initialized = true;

            Debug.Log($"[MNN] Localization initialized: {_currentLanguage}");
        }

        /// <summary>
        /// 检测Unity编辑器语言
        /// </summary>
        private static SystemLanguage DetectEditorLanguage()
        {
            // Unity编辑器会根据系统语言自动设置
            // 我们可以通过LocalizationDatabase来检测
            var editorLanguage = LocalizationDatabase.currentEditorLanguage;

            return editorLanguage switch
            {
                SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => SystemLanguage.Chinese,
                SystemLanguage.ChineseTraditional => SystemLanguage.ChineseTraditional,
                SystemLanguage.Japanese => SystemLanguage.Japanese,
                SystemLanguage.Korean => SystemLanguage.Korean,
                SystemLanguage.French => SystemLanguage.French,
                SystemLanguage.German => SystemLanguage.German,
                SystemLanguage.Spanish => SystemLanguage.Spanish,
                SystemLanguage.Russian => SystemLanguage.Russian,
                _ => SystemLanguage.English
            };
        }

        /// <summary>
        /// 获取翻译文本
        /// </summary>
        /// <param name="key">翻译键</param>
        /// <returns>翻译后的文本</returns>
        public static string Get(string key)
        {
            if (!_initialized)
                Initialize();

            if (_translations.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(_currentLanguage, out var translation))
                    return translation;

                // 回退到英文
                if (langDict.TryGetValue(SystemLanguage.English, out var fallback))
                    return fallback;
            }

            return key; // 如果找不到，返回key本身
        }

        /// <summary>
        /// 加载所有翻译
        /// </summary>
        private static void LoadTranslations()
        {
            _translations = new Dictionary<string, Dictionary<SystemLanguage, string>>();

            // 通用UI文本
            AddTranslation("common.ok", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "OK" },
                { SystemLanguage.Chinese, "确定" },
                { SystemLanguage.Japanese, "OK" },
                { SystemLanguage.Korean, "확인" }
            });

            AddTranslation("common.cancel", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Cancel" },
                { SystemLanguage.Chinese, "取消" },
                { SystemLanguage.Japanese, "キャンセル" },
                { SystemLanguage.Korean, "취소" }
            });

            AddTranslation("common.close", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Close" },
                { SystemLanguage.Chinese, "关闭" },
                { SystemLanguage.Japanese, "閉じる" },
                { SystemLanguage.Korean, "닫기" }
            });

            AddTranslation("common.refresh", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Refresh" },
                { SystemLanguage.Chinese, "刷新" },
                { SystemLanguage.Japanese, "更新" },
                { SystemLanguage.Korean, "새로고침" }
            });

            AddTranslation("common.download", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Download" },
                { SystemLanguage.Chinese, "下载" },
                { SystemLanguage.Japanese, "ダウンロード" },
                { SystemLanguage.Korean, "다운로드" }
            });

            AddTranslation("common.remove", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Remove" },
                { SystemLanguage.Chinese, "移除" },
                { SystemLanguage.Japanese, "削除" },
                { SystemLanguage.Korean, "제거" }
            });

            AddTranslation("common.search", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Search" },
                { SystemLanguage.Chinese, "搜索" },
                { SystemLanguage.Japanese, "検索" },
                { SystemLanguage.Korean, "검색" }
            });

            // 模型管理器
            AddTranslation("modelmanager.title", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "MNN Model Manager" },
                { SystemLanguage.Chinese, "MNN 模型管理器" },
                { SystemLanguage.Japanese, "MNN モデルマネージャー" },
                { SystemLanguage.Korean, "MNN 모델 관리자" }
            });

            AddTranslation("modelmanager.categories", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Categories" },
                { SystemLanguage.Chinese, "分类" },
                { SystemLanguage.Japanese, "カテゴリー" },
                { SystemLanguage.Korean, "카테고리" }
            });

            AddTranslation("modelmanager.models", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Models" },
                { SystemLanguage.Chinese, "模型" },
                { SystemLanguage.Japanese, "モデル" },
                { SystemLanguage.Korean, "모델" }
            });

            AddTranslation("modelmanager.details", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Model Details" },
                { SystemLanguage.Chinese, "模型详情" },
                { SystemLanguage.Japanese, "モデル詳細" },
                { SystemLanguage.Korean, "모델 세부정보" }
            });

            AddTranslation("modelmanager.openfolder", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Open Folder" },
                { SystemLanguage.Chinese, "打开文件夹" },
                { SystemLanguage.Japanese, "フォルダを開く" },
                { SystemLanguage.Korean, "폴더 열기" }
            });

            AddTranslation("modelmanager.selectmodel", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Select a model to view details" },
                { SystemLanguage.Chinese, "选择一个模型查看详情" },
                { SystemLanguage.Japanese, "モデルを選択して詳細を表示" },
                { SystemLanguage.Korean, "세부 정보를 보려면 모델을 선택하세요" }
            });

            AddTranslation("modelmanager.installed", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "installed" },
                { SystemLanguage.Chinese, "已安装" },
                { SystemLanguage.Japanese, "インストール済み" },
                { SystemLanguage.Korean, "설치됨" }
            });

            AddTranslation("modelmanager.locate", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Locate" },
                { SystemLanguage.Chinese, "定位" },
                { SystemLanguage.Japanese, "場所を表示" },
                { SystemLanguage.Korean, "찾기" }
            });

            // 模型分类
            AddTranslation("category.all", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "All Models" },
                { SystemLanguage.Chinese, "所有模型" },
                { SystemLanguage.Japanese, "すべてのモデル" },
                { SystemLanguage.Korean, "모든 모델" }
            });

            AddTranslation("category.imageclassification", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Image Classification" },
                { SystemLanguage.Chinese, "图像分类" },
                { SystemLanguage.Japanese, "画像分類" },
                { SystemLanguage.Korean, "이미지 분류" }
            });

            AddTranslation("category.objectdetection", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Object Detection" },
                { SystemLanguage.Chinese, "目标检测" },
                { SystemLanguage.Japanese, "物体検出" },
                { SystemLanguage.Korean, "객체 감지" }
            });

            AddTranslation("category.semanticsegmentation", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Semantic Segmentation" },
                { SystemLanguage.Chinese, "语义分割" },
                { SystemLanguage.Japanese, "セマンティックセグメンテーション" },
                { SystemLanguage.Korean, "의미론적 분할" }
            });

            AddTranslation("category.poseestimation", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Pose Estimation" },
                { SystemLanguage.Chinese, "姿态估计" },
                { SystemLanguage.Japanese, "姿勢推定" },
                { SystemLanguage.Korean, "자세 추정" }
            });

            AddTranslation("category.facedetection", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Face Detection" },
                { SystemLanguage.Chinese, "人脸检测" },
                { SystemLanguage.Japanese, "顔検出" },
                { SystemLanguage.Korean, "얼굴 감지" }
            });

            AddTranslation("category.facerecognition", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Face Recognition" },
                { SystemLanguage.Chinese, "人脸识别" },
                { SystemLanguage.Japanese, "顔認識" },
                { SystemLanguage.Korean, "얼굴 인식" }
            });

            AddTranslation("category.superresolution", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Super Resolution" },
                { SystemLanguage.Chinese, "超分辨率" },
                { SystemLanguage.Japanese, "超解像" },
                { SystemLanguage.Korean, "초해상도" }
            });

            AddTranslation("category.styletransfer", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Style Transfer" },
                { SystemLanguage.Chinese, "风格迁移" },
                { SystemLanguage.Japanese, "スタイル変換" },
                { SystemLanguage.Korean, "스타일 변환" }
            });

            // 模型详情字段
            AddTranslation("detail.name", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Name:" },
                { SystemLanguage.Chinese, "名称：" },
                { SystemLanguage.Japanese, "名前：" },
                { SystemLanguage.Korean, "이름:" }
            });

            AddTranslation("detail.category", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Category:" },
                { SystemLanguage.Chinese, "类别：" },
                { SystemLanguage.Japanese, "カテゴリー：" },
                { SystemLanguage.Korean, "카테고리:" }
            });

            AddTranslation("detail.version", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Version:" },
                { SystemLanguage.Chinese, "版本：" },
                { SystemLanguage.Japanese, "バージョン：" },
                { SystemLanguage.Korean, "버전:" }
            });

            AddTranslation("detail.author", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Author:" },
                { SystemLanguage.Chinese, "作者：" },
                { SystemLanguage.Japanese, "著者：" },
                { SystemLanguage.Korean, "작성자:" }
            });

            AddTranslation("detail.description", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Description:" },
                { SystemLanguage.Chinese, "描述：" },
                { SystemLanguage.Japanese, "説明：" },
                { SystemLanguage.Korean, "설명:" }
            });

            AddTranslation("detail.inputspec", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Input Specification:" },
                { SystemLanguage.Chinese, "输入规格：" },
                { SystemLanguage.Japanese, "入力仕様：" },
                { SystemLanguage.Korean, "입력 사양:" }
            });

            AddTranslation("detail.outputspec", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Output Specification:" },
                { SystemLanguage.Chinese, "输出规格：" },
                { SystemLanguage.Japanese, "出力仕様：" },
                { SystemLanguage.Korean, "출력 사양:" }
            });

            AddTranslation("detail.shape", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Shape:" },
                { SystemLanguage.Chinese, "形状：" },
                { SystemLanguage.Japanese, "形状：" },
                { SystemLanguage.Korean, "형태:" }
            });

            AddTranslation("detail.format", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Format:" },
                { SystemLanguage.Chinese, "格式：" },
                { SystemLanguage.Japanese, "フォーマット：" },
                { SystemLanguage.Korean, "형식:" }
            });

            AddTranslation("detail.type", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Type:" },
                { SystemLanguage.Chinese, "类型：" },
                { SystemLanguage.Japanese, "タイプ：" },
                { SystemLanguage.Korean, "유형:" }
            });

            AddTranslation("detail.classes", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Classes:" },
                { SystemLanguage.Chinese, "类别数：" },
                { SystemLanguage.Japanese, "クラス数：" },
                { SystemLanguage.Korean, "클래스:" }
            });

            AddTranslation("detail.filesize", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "File Size:" },
                { SystemLanguage.Chinese, "文件大小：" },
                { SystemLanguage.Japanese, "ファイルサイズ：" },
                { SystemLanguage.Korean, "파일 크기:" }
            });

            AddTranslation("detail.localpath", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Local Path:" },
                { SystemLanguage.Chinese, "本地路径：" },
                { SystemLanguage.Japanese, "ローカルパス：" },
                { SystemLanguage.Korean, "로컬 경로:" }
            });

            // 下载状态
            AddTranslation("download.downloading", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Downloading" },
                { SystemLanguage.Chinese, "下载中" },
                { SystemLanguage.Japanese, "ダウンロード中" },
                { SystemLanguage.Korean, "다운로드 중" }
            });

            AddTranslation("download.success", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "has been downloaded successfully!" },
                { SystemLanguage.Chinese, "下载成功！" },
                { SystemLanguage.Japanese, "のダウンロードが完了しました！" },
                { SystemLanguage.Korean, "다운로드가 완료되었습니다!" }
            });

            AddTranslation("download.failed", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Failed to download model:" },
                { SystemLanguage.Chinese, "下载模型失败：" },
                { SystemLanguage.Japanese, "モデルのダウンロードに失敗しました：" },
                { SystemLanguage.Korean, "모델 다운로드 실패:" }
            });

            AddTranslation("download.confirm", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Confirm" },
                { SystemLanguage.Chinese, "确认" },
                { SystemLanguage.Japanese, "確認" },
                { SystemLanguage.Korean, "확인" }
            });

            AddTranslation("download.remove.confirm", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Are you sure you want to remove" },
                { SystemLanguage.Chinese, "确定要移除" },
                { SystemLanguage.Japanese, "削除してもよろしいですか" },
                { SystemLanguage.Korean, "제거하시겠습니까" }
            });

            // 状态栏
            AddTranslation("status.models", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "models" },
                { SystemLanguage.Chinese, "个模型" },
                { SystemLanguage.Japanese, "個のモデル" },
                { SystemLanguage.Korean, "개 모델" }
            });

            AddTranslation("status.size", new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Size:" },
                { SystemLanguage.Chinese, "大小：" },
                { SystemLanguage.Japanese, "サイズ：" },
                { SystemLanguage.Korean, "크기:" }
            });
        }

        /// <summary>
        /// 添加翻译
        /// </summary>
        private static void AddTranslation(string key, Dictionary<SystemLanguage, string> translations)
        {
            _translations[key] = translations;
        }

        /// <summary>
        /// 格式化字符串（支持参数）
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            return string.Format(template, args);
        }
    }
}
