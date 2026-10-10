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
            if (_initialized)
                return;
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
            // 使用Application.systemLanguage作为基础
            var systemLanguage = Application.systemLanguage;
            // 尝试从EditorPrefs读取用户设置
            try
            {
                if (EditorPrefs.HasKey("MNN.EditorLanguage"))
                {
                    var langString = EditorPrefs.GetString("MNN.EditorLanguage");
                    if (System.Enum.TryParse<SystemLanguage>(langString, out var customLang))
                    {
                        return customLang;
                    }
                }
            }
            catch
            {
            // 继续使用系统语言
            }

            return systemLanguage switch
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
        /// <param name = "key">翻译键</param>
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
            AddTranslation("common.ok", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "OK"}, {SystemLanguage.Chinese, "确定"}, {SystemLanguage.Japanese, "OK"}, {SystemLanguage.Korean, "확인"}});
            AddTranslation("common.cancel", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Cancel"}, {SystemLanguage.Chinese, "取消"}, {SystemLanguage.Japanese, "キャンセル"}, {SystemLanguage.Korean, "취소"}});
            AddTranslation("common.close", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Close"}, {SystemLanguage.Chinese, "关闭"}, {SystemLanguage.Japanese, "閉じる"}, {SystemLanguage.Korean, "닫기"}});
            AddTranslation("common.refresh", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Refresh"}, {SystemLanguage.Chinese, "刷新"}, {SystemLanguage.Japanese, "更新"}, {SystemLanguage.Korean, "새로고침"}});
            AddTranslation("common.download", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Download"}, {SystemLanguage.Chinese, "下载"}, {SystemLanguage.Japanese, "ダウンロード"}, {SystemLanguage.Korean, "다운로드"}});
            AddTranslation("common.remove", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Remove"}, {SystemLanguage.Chinese, "移除"}, {SystemLanguage.Japanese, "削除"}, {SystemLanguage.Korean, "제거"}});
            AddTranslation("common.search", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Search"}, {SystemLanguage.Chinese, "搜索"}, {SystemLanguage.Japanese, "検索"}, {SystemLanguage.Korean, "검색"}});
            // 模型管理器
            AddTranslation("modelmanager.title", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "MNN Model Manager"}, {SystemLanguage.Chinese, "MNN 模型管理器"}, {SystemLanguage.Japanese, "MNN モデルマネージャー"}, {SystemLanguage.Korean, "MNN 모델 관리자"}});
            AddTranslation("modelmanager.categories", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Categories"}, {SystemLanguage.Chinese, "分类"}, {SystemLanguage.Japanese, "カテゴリー"}, {SystemLanguage.Korean, "카테고리"}});
            AddTranslation("modelmanager.models", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Models"}, {SystemLanguage.Chinese, "模型"}, {SystemLanguage.Japanese, "モデル"}, {SystemLanguage.Korean, "모델"}});
            AddTranslation("modelmanager.details", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Model Details"}, {SystemLanguage.Chinese, "模型详情"}, {SystemLanguage.Japanese, "モデル詳細"}, {SystemLanguage.Korean, "모델 세부정보"}});
            AddTranslation("modelmanager.openfolder", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Open Folder"}, {SystemLanguage.Chinese, "打开文件夹"}, {SystemLanguage.Japanese, "フォルダを開く"}, {SystemLanguage.Korean, "폴더 열기"}});
            AddTranslation("modelmanager.selectmodel", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Select a model to view details"}, {SystemLanguage.Chinese, "选择一个模型查看详情"}, {SystemLanguage.Japanese, "モデルを選択して詳細を表示"}, {SystemLanguage.Korean, "세부 정보를 보려면 모델을 선택하세요"}});
            AddTranslation("modelmanager.installed", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "installed"}, {SystemLanguage.Chinese, "已安装"}, {SystemLanguage.Japanese, "インストール済み"}, {SystemLanguage.Korean, "설치됨"}});
            AddTranslation("modelmanager.locate", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Locate"}, {SystemLanguage.Chinese, "定位"}, {SystemLanguage.Japanese, "場所を表示"}, {SystemLanguage.Korean, "찾기"}});
            AddTranslation("modelmanager.nomodels", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "No models found"}, {SystemLanguage.Chinese, "未找到模型"}, {SystemLanguage.Japanese, "モデルが見つかりません"}, {SystemLanguage.Korean, "모델을 찾을 수 없습니다"}});
            AddTranslation("modelmanager.loading", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Loading models from ModelScope…"}, {SystemLanguage.Chinese, "正在从 ModelScope 加载模型…"}, {SystemLanguage.Japanese, "ModelScope からモデルを読み込み中…"}, {SystemLanguage.Korean, "ModelScope에서 모델을 불러오는 중…"}});
            AddTranslation("modelmanager.type", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Type"}, {SystemLanguage.Chinese, "类型"}, {SystemLanguage.Japanese, "種類"}, {SystemLanguage.Korean, "유형"}});
            AddTranslation("modelmanager.series", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Series"}, {SystemLanguage.Chinese, "系列"}, {SystemLanguage.Japanese, "シリーズ"}, {SystemLanguage.Korean, "시리즈"}});
            AddTranslation("modelmanager.generation", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Generation"}, {SystemLanguage.Chinese, "代际"}, {SystemLanguage.Japanese, "世代"}, {SystemLanguage.Korean, "세대"}});
            AddTranslation("modelmanager.size", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Size"}, {SystemLanguage.Chinese, "大小"}, {SystemLanguage.Japanese, "サイズ"}, {SystemLanguage.Korean, "크기"}});
            AddTranslation("modelmanager.parameters", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Params"}, {SystemLanguage.Chinese, "参数量"}, {SystemLanguage.Japanese, "パラメーター"}, {SystemLanguage.Korean, "파라미터"}});
            AddTranslation("modelmanager.sort", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Sort"}, {SystemLanguage.Chinese, "排序"}, {SystemLanguage.Japanese, "並べ替え"}, {SystemLanguage.Korean, "정렬"}});
            AddFilterTranslation("filter.allseries", "All series", "全部系列", "すべてのシリーズ", "모든 시리즈");
            AddFilterTranslation("filter.allgenerations", "All generations", "全部代际", "すべての世代", "모든 세대");
            AddFilterTranslation("filter.inferred", "estimated", "名称推断", "名前から推定", "이름에서 추정");
            AddFilterTranslation("filter.advanced", "Size and parameter filters", "大小与参数筛选", "サイズ・パラメーターで絞り込む", "크기 및 파라미터 필터");
            AddFilterTranslation("filter.advancedactive", "Filters applied", "筛选已生效", "フィルター適用中", "필터 적용 중");
            AddFilterTranslation("filter.size.any", "Any size", "全部大小", "すべてのサイズ", "모든 크기");
            AddFilterTranslation("filter.size.under100mb", "Under 100 MB", "小于 100 MB", "100 MB 未満", "100 MB 미만");
            AddFilterTranslation("filter.size.100mb1gb", "100 MB - 1 GB", "100 MB - 1 GB", "100 MB - 1 GB", "100 MB - 1 GB");
            AddFilterTranslation("filter.size.1gb5gb", "1 - 5 GB", "1 - 5 GB", "1 - 5 GB", "1 - 5 GB");
            AddFilterTranslation("filter.size.over5gb", "Over 5 GB", "大于 5 GB", "5 GB 超", "5 GB 초과");
            AddFilterTranslation("filter.params.any", "Any parameter count", "全部参数量", "すべてのパラメーター数", "모든 파라미터 수");
            AddFilterTranslation("filter.params.under1b", "Under 1B", "小于 1B", "1B 未満", "1B 미만");
            AddFilterTranslation("filter.params.1b3b", "1B - 3B", "1B - 3B", "1B - 3B", "1B - 3B");
            AddFilterTranslation("filter.params.3b7b", "3B - 7B", "3B - 7B", "3B - 7B", "3B - 7B");
            AddFilterTranslation("filter.params.over7b", "7B and up", "7B 及以上", "7B 以上", "7B 이상");
            AddFilterTranslation("sort.relevance", "ModelScope order", "ModelScope 默认顺序", "ModelScope 順", "ModelScope 순서");
            AddFilterTranslation("sort.name", "Name A-Z", "名称 A-Z", "名前 A-Z", "이름 A-Z");
            AddFilterTranslation("sort.sizeascending", "Size: small to large", "大小：从小到大", "サイズ：小さい順", "크기: 작은 순");
            AddFilterTranslation("sort.sizedescending", "Size: large to small", "大小：从大到小", "サイズ：大きい順", "크기: 큰 순");
            AddFilterTranslation("sort.paramsascending", "Params: small to large", "参数量：从小到大", "パラメーター：少ない順", "파라미터: 작은 순");
            AddFilterTranslation("sort.paramsdescending", "Params: large to small", "参数量：从大到小", "パラメーター：多い順", "파라미터: 큰 순");
            AddFilterTranslation("sort.downloads", "Most downloaded", "下载量从高到低", "ダウンロード数順", "다운로드 수 순");
            AddTranslation("common.error", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Error"}, {SystemLanguage.Chinese, "错误"}, {SystemLanguage.Japanese, "エラー"}, {SystemLanguage.Korean, "오류"}});
            // 模型分类
            AddTranslation("category.all", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "All Models"}, {SystemLanguage.Chinese, "所有模型"}, {SystemLanguage.Japanese, "すべてのモデル"}, {SystemLanguage.Korean, "모든 모델"}});
            AddTranslation("category.imageclassification", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Image Classification"}, {SystemLanguage.Chinese, "图像分类"}, {SystemLanguage.Japanese, "画像分類"}, {SystemLanguage.Korean, "이미지 분류"}});
            AddTranslation("category.objectdetection", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Object Detection"}, {SystemLanguage.Chinese, "目标检测"}, {SystemLanguage.Japanese, "物体検出"}, {SystemLanguage.Korean, "객체 감지"}});
            AddTranslation("category.semanticsegmentation", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Semantic Segmentation"}, {SystemLanguage.Chinese, "语义分割"}, {SystemLanguage.Japanese, "セマンティックセグメンテーション"}, {SystemLanguage.Korean, "의미론적 분할"}});
            AddTranslation("category.poseestimation", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Pose Estimation"}, {SystemLanguage.Chinese, "姿态估计"}, {SystemLanguage.Japanese, "姿勢推定"}, {SystemLanguage.Korean, "자세 추정"}});
            AddTranslation("category.facedetection", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Face Detection"}, {SystemLanguage.Chinese, "人脸检测"}, {SystemLanguage.Japanese, "顔検出"}, {SystemLanguage.Korean, "얼굴 감지"}});
            AddTranslation("category.facerecognition", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Face Recognition"}, {SystemLanguage.Chinese, "人脸识别"}, {SystemLanguage.Japanese, "顔認識"}, {SystemLanguage.Korean, "얼굴 인식"}});
            AddTranslation("category.superresolution", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Super Resolution"}, {SystemLanguage.Chinese, "超分辨率"}, {SystemLanguage.Japanese, "超解像"}, {SystemLanguage.Korean, "초해상도"}});
            AddTranslation("category.styletransfer", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Style Transfer"}, {SystemLanguage.Chinese, "风格迁移"}, {SystemLanguage.Japanese, "スタイル変換"}, {SystemLanguage.Korean, "스타일 변환"}});
            AddTranslation("category.largelanguagemodel", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Large Language Models"}, {SystemLanguage.Chinese, "大语言模型"}, {SystemLanguage.Japanese, "大規模言語モデル"}, {SystemLanguage.Korean, "대규모 언어 모델"}});
            AddFilterTranslation("category.visionlanguagemodel", "Vision-language models", "视觉语言模型", "視覚言語モデル", "비전 언어 모델");
            AddFilterTranslation("category.omnimodel", "Omni-modal models", "全模态模型", "オムニモーダルモデル", "옴니모달 모델");
            AddFilterTranslation("category.audiolanguagemodel", "Audio-language models", "音频语言模型", "音声言語モデル", "오디오 언어 모델");
            AddFilterTranslation("category.speechsynthesis", "Speech synthesis (TTS)", "语音合成（TTS）", "音声合成（TTS）", "음성 합성 (TTS)");
            AddFilterTranslation("category.speechrecognition", "Speech recognition (ASR)", "语音识别（ASR）", "音声認識（ASR）", "음성 인식 (ASR)");
            AddFilterTranslation("category.embeddingmodel", "Embedding models", "Embedding 模型", "埋め込みモデル", "임베딩 모델");
            AddFilterTranslation("category.reranker", "Rerankers", "重排序模型", "再ランキングモデル", "재순위 모델");
            AddFilterTranslation("category.ocrmodel", "OCR models", "OCR 模型", "OCR モデル", "OCR 모델");
            AddFilterTranslation("category.codemodel", "Code models", "代码模型", "コードモデル", "코드 모델");
            AddFilterTranslation("category.safetymodel", "Safety models", "安全审核模型", "安全性モデル", "안전 모델");
            AddFilterTranslation("category.imagegenerationmodel", "Image generation models", "图像生成模型", "画像生成モデル", "이미지 생성 모델");
            AddFilterTranslation("category.textgeneration", "Text generation", "文本生成", "テキスト生成", "텍스트 생성");
            AddFilterTranslation("category.nlp", "Natural language processing", "自然语言处理", "自然言語処理", "자연어 처리");
            AddFilterTranslation("category.audioprocessing", "Audio processing", "音频处理", "音声処理", "오디오 처리");
            AddFilterTranslation("category.ocr", "OCR", "OCR", "OCR", "OCR");
            AddFilterTranslation("category.other", "Other models", "其他模型", "その他のモデル", "기타 모델");
            // 模型详情字段
            AddTranslation("detail.name", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Name:"}, {SystemLanguage.Chinese, "名称："}, {SystemLanguage.Japanese, "名前："}, {SystemLanguage.Korean, "이름:"}});
            AddTranslation("detail.category", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Category:"}, {SystemLanguage.Chinese, "类别："}, {SystemLanguage.Japanese, "カテゴリー："}, {SystemLanguage.Korean, "카테고리:"}});
            AddTranslation("detail.version", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Version:"}, {SystemLanguage.Chinese, "版本："}, {SystemLanguage.Japanese, "バージョン："}, {SystemLanguage.Korean, "버전:"}});
            AddTranslation("detail.author", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Author:"}, {SystemLanguage.Chinese, "作者："}, {SystemLanguage.Japanese, "著者："}, {SystemLanguage.Korean, "작성자:"}});
            AddTranslation("detail.description", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Description:"}, {SystemLanguage.Chinese, "描述："}, {SystemLanguage.Japanese, "説明："}, {SystemLanguage.Korean, "설명:"}});
            AddTranslation("detail.inputspec", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Input Specification:"}, {SystemLanguage.Chinese, "输入规格："}, {SystemLanguage.Japanese, "入力仕様："}, {SystemLanguage.Korean, "입력 사양:"}});
            AddTranslation("detail.outputspec", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Output Specification:"}, {SystemLanguage.Chinese, "输出规格："}, {SystemLanguage.Japanese, "出力仕様："}, {SystemLanguage.Korean, "출력 사양:"}});
            AddTranslation("detail.shape", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Shape:"}, {SystemLanguage.Chinese, "形状："}, {SystemLanguage.Japanese, "形状："}, {SystemLanguage.Korean, "형태:"}});
            AddTranslation("detail.format", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Format:"}, {SystemLanguage.Chinese, "格式："}, {SystemLanguage.Japanese, "フォーマット："}, {SystemLanguage.Korean, "형식:"}});
            AddTranslation("detail.type", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Type:"}, {SystemLanguage.Chinese, "类型："}, {SystemLanguage.Japanese, "タイプ："}, {SystemLanguage.Korean, "유형:"}});
            AddTranslation("detail.classes", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Classes:"}, {SystemLanguage.Chinese, "类别数："}, {SystemLanguage.Japanese, "クラス数："}, {SystemLanguage.Korean, "클래스:"}});
            AddTranslation("detail.filesize", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "File Size:"}, {SystemLanguage.Chinese, "文件大小："}, {SystemLanguage.Japanese, "ファイルサイズ："}, {SystemLanguage.Korean, "파일 크기:"}});
            AddTranslation("detail.downloads", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Downloads:"}, {SystemLanguage.Chinese, "下载量："}, {SystemLanguage.Japanese, "ダウンロード数："}, {SystemLanguage.Korean, "다운로드 수:"}});
            AddTranslation("detail.localpath", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Local Path:"}, {SystemLanguage.Chinese, "本地路径："}, {SystemLanguage.Japanese, "ローカルパス："}, {SystemLanguage.Korean, "로컬 경로:"}});
            // 下载状态
            AddFilterTranslation("download.pause", "Pause", "暂停", "一時停止", "일시 정지");
            AddFilterTranslation("download.resume", "Resume", "继续", "再開", "계속");
            AddFilterTranslation("download.dismiss", "Dismiss", "收起", "閉じる", "닫기");
            AddFilterTranslation("download.tasks", "Downloads", "下载任务", "ダウンロード", "다운로드");
            AddFilterTranslation("download.pausing", "Pausing…", "正在暂停…", "停止中…", "일시 정지 중…");
            AddFilterTranslation("download.paused", "Paused · resume available", "已暂停 · 可继续下载", "一時停止 · 再開可能", "일시 정지 · 재개 가능");
            AddFilterTranslation("download.cancelling", "Cancelling…", "正在取消…", "キャンセル中…", "취소 중…");
            AddFilterTranslation("download.cancelled", "Cancelled", "已取消", "キャンセル済み", "취소됨");
            AddFilterTranslation("download.completed", "Downloaded", "下载完成", "完了", "완료");
            AddTranslation("download.downloading", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Downloading"}, {SystemLanguage.Chinese, "下载中"}, {SystemLanguage.Japanese, "ダウンロード中"}, {SystemLanguage.Korean, "다운로드 중"}});
            AddTranslation("download.success", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "has been downloaded successfully!"}, {SystemLanguage.Chinese, "下载成功！"}, {SystemLanguage.Japanese, "のダウンロードが完了しました！"}, {SystemLanguage.Korean, "다운로드가 완료되었습니다!"}});
            AddTranslation("download.failed", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Failed to download model:"}, {SystemLanguage.Chinese, "下载模型失败："}, {SystemLanguage.Japanese, "モデルのダウンロードに失敗しました："}, {SystemLanguage.Korean, "모델 다운로드 실패:"}});
            AddTranslation("download.confirm", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Confirm"}, {SystemLanguage.Chinese, "确认"}, {SystemLanguage.Japanese, "確認"}, {SystemLanguage.Korean, "확인"}});
            AddTranslation("download.remove.confirm", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Are you sure you want to remove"}, {SystemLanguage.Chinese, "确定要移除"}, {SystemLanguage.Japanese, "削除してもよろしいですか"}, {SystemLanguage.Korean, "제거하시겠습니까"}});
            // 状态栏
            AddTranslation("status.models", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "models"}, {SystemLanguage.Chinese, "个模型"}, {SystemLanguage.Japanese, "個のモデル"}, {SystemLanguage.Korean, "개 모델"}});
            AddTranslation("status.size", new Dictionary<SystemLanguage, string>{{SystemLanguage.English, "Size:"}, {SystemLanguage.Chinese, "大小："}, {SystemLanguage.Japanese, "サイズ："}, {SystemLanguage.Korean, "크기:"}});
        }

        /// <summary>
        /// 添加翻译
        /// </summary>
        private static void AddTranslation(string key, Dictionary<SystemLanguage, string> translations)
        {
            _translations[key] = translations;
        }

        private static void AddFilterTranslation(string key, string english, string chinese, string japanese, string korean)
        {
            AddTranslation(key, new Dictionary<SystemLanguage, string>{{SystemLanguage.English, english}, {SystemLanguage.Chinese, chinese}, {SystemLanguage.Japanese, japanese}, {SystemLanguage.Korean, korean}});
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
