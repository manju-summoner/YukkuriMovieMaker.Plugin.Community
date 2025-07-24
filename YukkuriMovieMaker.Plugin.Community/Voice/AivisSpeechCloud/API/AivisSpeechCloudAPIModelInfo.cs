using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    /*
{
  "aivm_model_uuid": "69ab2924-38bd-4054-acd8-91a237e728e1",
  "user": {
    "handle": "^aaa$",
    "name": "string",
    "description": "string",
    "icon_url": "http://example.com",
    "account_type": "User",
    "account_status": "Active",
    "social_links": [
      {
        "type": "Twitter",
        "url": "http://example.com"
      }
    ]
  },
  "name": "string",
  "description": "string",
  "detailed_description": "string",
  "category": "ExistingCharacter",
  "voice_timbre": "YoungMale",
  "visibility": "Public",
  "is_tag_locked": true,
  "total_download_count": 0,
  "model_files": [
    {
      "aivm_model_uuid": "69ab2924-38bd-4054-acd8-91a237e728e1",
      "manifest_version": "string",
      "name": "string",
      "description": "string",
      "creators": [
        "string"
      ],
      "license_type": "ACML 1.0",
      "license_text": "string",
      "model_type": "AIVM",
      "model_architecture": "Style-Bert-VITS2",
      "model_format": "Safetensors",
      "training_epochs": 0,
      "training_steps": 0,
      "version": "string",
      "file_size": 0,
      "checksum": "string",
      "download_count": 0,
      "created_at": "2019-08-24T14:15:22Z",
      "updated_at": "2019-08-24T14:15:22Z"
    }
  ],
  "tags": [
    {
      "name": "string"
    }
  ],
  "like_count": 0,
  "is_liked": true,
  "speakers": [
    {
      "aivm_speaker_uuid": "80d4cd16-4b84-4d9d-81c0-403e6a3e29e6",
      "name": "string",
      "icon_url": "string",
      "supported_languages": [
        "string"
      ],
      "local_id": 0,
      "styles": [
        {
          "name": "string",
          "icon_url": "string",
          "local_id": 0,
          "voice_samples": [
            {
              "audio_url": "string",
              "transcript": "string"
            }
          ]
        }
      ]
    }
  ],
  "created_at": "2019-08-24T14:15:22Z",
  "updated_at": "2019-08-24T14:15:22Z"
}
    */
    public record AivisSpeechCloudAPIModelInfo(
        [property: JsonProperty("aivm_model_uuid")] string AivmModelUuid,
        [property: JsonProperty("user")] AivisSpeechCloudAPIUserInfo User,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("description")] string Description,
        [property: JsonProperty("detailed_description")] string DetailedDescription,
        [property: JsonProperty("category")] string Category,
        [property: JsonProperty("voice_timbre")] string VoiceTimbre,
        [property: JsonProperty("visibility")] string Visibility,
        [property: JsonProperty("is_tag_locked")] bool IsTagLocked,
        [property: JsonProperty("total_download_count")] int TotalDownloadCount,
        [property: JsonProperty("model_files")] List<AivisSpeechCloudAPIModelFileInfo> ModelFiles,
        [property: JsonProperty("tags")] List<AivisSpeechCloudAPITag> Tags,
        [property: JsonProperty("like_count")] int LikeCount,
        [property: JsonProperty("is_liked")] bool IsLiked,
        [property: JsonProperty("speakers")] List<AivisSpeechCloudAPISpeaker> Speakers,
        [property: JsonProperty("created_at")] DateTime CreatedAt,
        [property: JsonProperty("updated_at")] DateTime UpdatedAt
        )
    {
        static readonly string[] DefaultModels = [
            //Anneli
            "{\"aivm_model_uuid\":\"a59cb814-0083-4369-8542-f51a29e72af7\",\"user\":{\"handle\":\"aivis_project\",\"name\":\"Aivis Project\",\"description\":\"Aivis Project の公式アカウントです。\\n音声合成モデルの公式配布を行なっています。\",\"icon_url\":\"https://assets.aivis-project.com/account-icons/3889b76f-2c8b-41aa-a359-fa6603fdbf60.jpg\",\"account_type\":\"Admin\",\"account_status\":\"Active\",\"social_links\":[{\"type\":\"Twitter\",\"url\":\"https://x.com/aivis_project\"},{\"type\":\"GitHub\",\"url\":\"https://github.com/Aivis-Project\"},{\"type\":\"Website\",\"url\":\"https://note.com/jpchain/\"},{\"type\":\"Website\",\"url\":\"https://aivis-project.com/\"}]},\"name\":\"Anneli\",\"description\":\"AivisSpeech に標準搭載されている音声合成モデルです。\",\"detailed_description\":\"\",\"category\":\"OriginalCharacter\",\"voice_timbre\":\"YouthfulFemale\",\"visibility\":\"Public\",\"is_tag_locked\":false,\"total_download_count\":63683,\"model_files\":[{\"aivm_model_uuid\":\"a59cb814-0083-4369-8542-f51a29e72af7\",\"manifest_version\":\"1.0\",\"name\":\"Anneli\",\"description\":\"AivisSpeech に標準搭載されている音声合成モデルです。\",\"creators\":[\"Aivis Project\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVM\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"Safetensors\",\"training_epochs\":116,\"training_steps\":32000,\"version\":\"1.0.1\",\"file_size\":257639874,\"checksum\":\"51a69c4218b73218a5066535eca9545fcc4154409cd157fd357bf42bed409ae6\",\"download_count\":184,\"created_at\":\"2025-04-06T07:59:47.064137+09:00\",\"updated_at\":\"2025-07-13T19:49:54.016722+09:00\"},{\"aivm_model_uuid\":\"a59cb814-0083-4369-8542-f51a29e72af7\",\"manifest_version\":\"1.0\",\"name\":\"Anneli\",\"description\":\"AivisSpeech に標準搭載されている音声合成モデルです。\",\"creators\":[\"Aivis Project\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVMX\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"ONNX\",\"training_epochs\":116,\"training_steps\":32000,\"version\":\"1.0.1\",\"file_size\":255887473,\"checksum\":\"1f7898b4660fbcac7aa2e259122cb2f0ff74b8e6289472ff95f968c5a89e6a7d\",\"download_count\":63510,\"created_at\":\"2025-04-06T07:59:57.522737+09:00\",\"updated_at\":\"2025-07-24T13:16:53.557133+09:00\"}],\"tags\":[],\"like_count\":107,\"is_liked\":false,\"speakers\":[{\"aivm_speaker_uuid\":\"e756b8e4-b606-4e15-99b1-3f9c6a1b2317\",\"name\":\"Anneli\",\"icon_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/icon.jpg\",\"supported_languages\":[\"ja\"],\"local_id\":0,\"styles\":[{\"name\":\"ノーマル\",\"icon_url\":null,\"local_id\":0,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/213b9ab0-e743-4361-bec0-64bc8005487e/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/213b9ab0-e743-4361-bec0-64bc8005487e/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/213b9ab0-e743-4361-bec0-64bc8005487e/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　…………そっか…。コロナ流行ってるもんね。じゃまた今度にしようか。…元気になったらぜひご飯でも！\"}]},{\"name\":\"通常\",\"icon_url\":null,\"local_id\":1,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/cec90d85-f738-4952-b1b2-e383c1accd44/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/cec90d85-f738-4952-b1b2-e383c1accd44/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/cec90d85-f738-4952-b1b2-e383c1accd44/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　…………そっか…。コロナ流行ってるもんね。じゃまた今度にしようか。…元気になったらぜひご飯でも！\"}]},{\"name\":\"テンション高め\",\"icon_url\":null,\"local_id\":2,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/fb812c10-9674-4845-9110-7a27562166f5/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/fb812c10-9674-4845-9110-7a27562166f5/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/fb812c10-9674-4845-9110-7a27562166f5/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　…………そっか…。コロナ流行ってるもんね。じゃまた今度にしようか。…元気になったらぜひご飯でも！\"}]},{\"name\":\"上機嫌\",\"icon_url\":null,\"local_id\":4,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/41c3f7f7-ba95-4898-9a0d-92f51f0398b4/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/41c3f7f7-ba95-4898-9a0d-92f51f0398b4/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/41c3f7f7-ba95-4898-9a0d-92f51f0398b4/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　………そっか…。コロナ流行ってるもんね。じゃまた今度にしようかー。元気になったらぜひご飯でも！\"}]},{\"name\":\"落ち着き\",\"icon_url\":null,\"local_id\":3,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/432bfa35-80a6-4ba4-b020-bafdf984b044/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/432bfa35-80a6-4ba4-b020-bafdf984b044/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/432bfa35-80a6-4ba4-b020-bafdf984b044/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　…………そっか…。コロナ流行ってるもんね。じゃまた今度にしようか。…元気になったらぜひご飯でも！\"}]},{\"name\":\"怒り・悲しみ\",\"icon_url\":null,\"local_id\":5,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/8bdb0452-973c-481e-9e05-e553cc13f5a4/voice-samples/0.m4a\",\"transcript\":\"おはようございます！現在時刻は7時30分です。今日の東京の気温は18度で、天気は雨です。10時からミーティング、午後3時に歯医者の予約があります。今日も素敵な一日になりますように。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/8bdb0452-973c-481e-9e05-e553cc13f5a4/voice-samples/1.m4a\",\"transcript\":\"やった〜！テストでようやく満点取れた〜！めちゃくちゃ嬉しい…。　そうそう、さっき読んでたこの漫画がめっちゃ面白くてさ〜！見てよこれ！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/5b4c4479-abea-4436-ae82-cb1860aa6ba1/speakers/d2010996-4ef7-4ca9-8438-a585fbabaacf/styles/8bdb0452-973c-481e-9e05-e553cc13f5a4/voice-samples/2.m4a\",\"transcript\":\"ごめんね、今ちょっと風邪気味なんだよね…。それでもよければ会いたいけど、どう？　………そっか…。コロナ流行ってるもんね。じゃまた今度にしようかー。元気になったらぜひご飯でも！\"}]}]}],\"created_at\":\"2024-11-19T23:19:24.340649+09:00\",\"updated_at\":\"2025-07-24T13:16:53.559127+09:00\"}",
            //凛音エル
            "{\"aivm_model_uuid\":\"f5017410-fbb5-49e1-97cb-e785f42e15f5\",\"user\":{\"handle\":\"kokuren\",\"name\":\"kokuren\",\"description\":\"電脳天使工業（Cyber Angel Industry）\",\"icon_url\":\"https://assets.aivis-project.com/account-icons/eaa70315-7ebf-4c3d-b4d9-4ae381b7aab5.jpg\",\"account_type\":\"User\",\"account_status\":\"Active\",\"social_links\":[]},\"name\":\"凛音エル\",\"description\":\"電脳天使工業のAIキャラクター「凛音エル」のボイスモデルです。\",\"detailed_description\":\"\",\"category\":\"OriginalCharacter\",\"voice_timbre\":\"YouthfulFemale\",\"visibility\":\"Public\",\"is_tag_locked\":false,\"total_download_count\":6204,\"model_files\":[{\"aivm_model_uuid\":\"f5017410-fbb5-49e1-97cb-e785f42e15f5\",\"manifest_version\":\"1.0\",\"name\":\"凛音エル\",\"description\":\"電脳天使工業のAIキャラクター「凛音エル」のボイスモデルです。\",\"creators\":[\"kokuren\",\"電脳天使工業\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVM\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"Safetensors\",\"training_epochs\":null,\"training_steps\":5000,\"version\":\"1.0.0\",\"file_size\":254502766,\"checksum\":\"b9850f7f5db2f97bdfbce96edbdc4f854b137d8a1924ea78d208948c6047e588\",\"download_count\":31,\"created_at\":\"2024-12-09T06:18:33.929077+09:00\",\"updated_at\":\"2025-07-18T14:28:11.643594+09:00\"},{\"aivm_model_uuid\":\"f5017410-fbb5-49e1-97cb-e785f42e15f5\",\"manifest_version\":\"1.0\",\"name\":\"凛音エル\",\"description\":\"電脳天使工業のAIキャラクター「凛音エル」のボイスモデルです。\",\"creators\":[\"kokuren\",\"電脳天使工業\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVMX\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"ONNX\",\"training_epochs\":null,\"training_steps\":5000,\"version\":\"1.0.0\",\"file_size\":252769346,\"checksum\":\"e7ac9e2636f59d6f6512016d0752f9e9f89c2b3ffd2012a6a1b92791330e23bc\",\"download_count\":6174,\"created_at\":\"2024-12-09T06:18:41.270584+09:00\",\"updated_at\":\"2025-07-24T11:16:53.529515+09:00\"}],\"tags\":[],\"like_count\":42,\"is_liked\":false,\"speakers\":[{\"aivm_speaker_uuid\":\"d2c99ca6-73e5-486c-994e-ee0ce2d74928\",\"name\":\"凛音エル\",\"icon_url\":\"https://assets.aivis-project.com/aivm-models/9fc6e37f-b8d4-4df5-93e3-30defd0f00ff/speakers/6b0a0fb6-283c-4b7e-928d-37fda7a639bb/icon.jpg\",\"supported_languages\":[\"ja\"],\"local_id\":0,\"styles\":[{\"name\":\"ノーマル\",\"icon_url\":null,\"local_id\":0,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/9fc6e37f-b8d4-4df5-93e3-30defd0f00ff/speakers/6b0a0fb6-283c-4b7e-928d-37fda7a639bb/styles/17203929-af8f-47cd-8faf-124a14786f8f/voice-samples/0.wav\",\"transcript\":\"こんにちは！私の名前はエルです。何かお手伝いが必要でしたら、いつでも気軽に聞いてくださいね！\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/9fc6e37f-b8d4-4df5-93e3-30defd0f00ff/speakers/6b0a0fb6-283c-4b7e-928d-37fda7a639bb/styles/17203929-af8f-47cd-8faf-124a14786f8f/voice-samples/1.wav\",\"transcript\":\"今日はとてもいい天気ですね！素敵な一日をお過ごしください。もし何かできることがあれば、ぜひ教えてくださいね。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/9fc6e37f-b8d4-4df5-93e3-30defd0f00ff/speakers/6b0a0fb6-283c-4b7e-928d-37fda7a639bb/styles/17203929-af8f-47cd-8faf-124a14786f8f/voice-samples/2.wav\",\"transcript\":\"テクノロジーが進化し続ける世界で、私たちの生活はますますつながりを深めています。これは、革新がどのように未来を形作るかを語る物語です。\"}]},{\"name\":\"Angry\",\"icon_url\":null,\"local_id\":1,\"voice_samples\":[]},{\"name\":\"Fear\",\"icon_url\":null,\"local_id\":2,\"voice_samples\":[]},{\"name\":\"Happy\",\"icon_url\":null,\"local_id\":3,\"voice_samples\":[]},{\"name\":\"Sad\",\"icon_url\":null,\"local_id\":4,\"voice_samples\":[]}]}],\"created_at\":\"2024-12-09T06:18:33.925751+09:00\",\"updated_at\":\"2025-07-24T11:16:53.530497+09:00\"}",
            //阿井田 茂
            "{\"aivm_model_uuid\":\"47e53151-a378-46f3-abee-ce13aa07feb1\",\"user\":{\"handle\":\"khirio\",\"name\":\"古山キリヲ\",\"description\":\"狂気のデータサイエンティスト\",\"icon_url\":\"https://assets.aivis-project.com/account-icons/6d46593e-e93e-44d4-a27f-93175e0189ce.jpg\",\"account_type\":\"User\",\"account_status\":\"Active\",\"social_links\":[{\"type\":\"Website\",\"url\":\"https://sites.google.com/view/khirio-altenberg/home\"},{\"type\":\"Twitter\",\"url\":\"https://x.com/kuwa0611\"}]},\"name\":\"阿井田 茂\",\"description\":\"阿井田 茂（あいだ しげる）　\\nCV：古山キリヲ\\n\\n通称：モアイさん\\nバリトンボイスの朗らかおじさん\",\"detailed_description\":\"\",\"category\":\"OriginalCharacter\",\"voice_timbre\":\"MiddleAgedMale\",\"visibility\":\"Public\",\"is_tag_locked\":false,\"total_download_count\":5528,\"model_files\":[{\"aivm_model_uuid\":\"47e53151-a378-46f3-abee-ce13aa07feb1\",\"manifest_version\":\"1.0\",\"name\":\"阿井田 茂\",\"description\":\"阿井田 茂（あいだ しげる）　\\nCV：古山キリヲ\\n\\n通称：モアイさん\\nバリトンボイスの朗らかおじさん\",\"creators\":[\"古山キリヲ\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVM\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"Safetensors\",\"training_epochs\":300,\"training_steps\":3600,\"version\":\"1.0.0\",\"file_size\":252870536,\"checksum\":\"799cbe0ebbf79e13756f0c95395afa07db0fd9c6e0c078ce632331a3e5c14557\",\"download_count\":5,\"created_at\":\"2025-04-02T00:53:01.442551+09:00\",\"updated_at\":\"2025-06-08T08:18:11.196196+09:00\"},{\"aivm_model_uuid\":\"47e53151-a378-46f3-abee-ce13aa07feb1\",\"manifest_version\":\"1.0\",\"name\":\"阿井田 茂\",\"description\":\"阿井田 茂（あいだ しげる）　\\nCV：古山キリヲ\\n\\n通称：モアイさん\\nバリトンボイスの朗らかおじさん\",\"creators\":[\"古山キリヲ\"],\"license_type\":\"ACML 1.0\",\"license_text\":null,\"model_type\":\"AIVMX\",\"model_architecture\":\"Style-Bert-VITS2 (JP-Extra)\",\"model_format\":\"ONNX\",\"training_epochs\":300,\"training_steps\":3600,\"version\":\"1.0.0\",\"file_size\":251137086,\"checksum\":\"6dabe29de5ec2c1715e12a430805e1bff6ec64a315cccec2d26fad029df83243\",\"download_count\":5523,\"created_at\":\"2025-04-02T00:53:11.664557+09:00\",\"updated_at\":\"2025-07-24T13:31:29.391597+09:00\"}],\"tags\":[{\"name\":\"文化系心臓部\"}],\"like_count\":37,\"is_liked\":false,\"speakers\":[{\"aivm_speaker_uuid\":\"561e4e59-3bc9-4726-9028-44a3c12a6f1d\",\"name\":\"阿井田 茂\",\"icon_url\":\"https://assets.aivis-project.com/aivm-models/d799f1c0-59f3-4b6b-9a65-56715776fc69/speakers/50880d3a-d63e-4a31-ae3f-ab14e0c0b3cb/icon.jpg\",\"supported_languages\":[\"ja\"],\"local_id\":0,\"styles\":[{\"name\":\"ノーマル\",\"icon_url\":null,\"local_id\":0,\"voice_samples\":[{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/d799f1c0-59f3-4b6b-9a65-56715776fc69/speakers/50880d3a-d63e-4a31-ae3f-ab14e0c0b3cb/styles/1d5aedb4-26c9-4412-b76a-80a4ff5f5b2d/voice-samples/0.wav\",\"transcript\":\"そのグルガン族の男は、静かに語った。\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/d799f1c0-59f3-4b6b-9a65-56715776fc69/speakers/50880d3a-d63e-4a31-ae3f-ab14e0c0b3cb/styles/1d5aedb4-26c9-4412-b76a-80a4ff5f5b2d/voice-samples/1.wav\",\"transcript\":\"合成音声で、推しとのボイスドラマが、合法的に、簡単に作れる！いい時代になったものだ…\"},{\"audio_url\":\"https://assets.aivis-project.com/aivm-models/d799f1c0-59f3-4b6b-9a65-56715776fc69/speakers/50880d3a-d63e-4a31-ae3f-ab14e0c0b3cb/styles/1d5aedb4-26c9-4412-b76a-80a4ff5f5b2d/voice-samples/2.wav\",\"transcript\":\"流石の私も…そればっかりはどうかと思うなあ～！？\"}]},{\"name\":\"Calm\",\"icon_url\":null,\"local_id\":1,\"voice_samples\":[]},{\"name\":\"Far\",\"icon_url\":null,\"local_id\":2,\"voice_samples\":[]},{\"name\":\"Heavy\",\"icon_url\":null,\"local_id\":3,\"voice_samples\":[]},{\"name\":\"Mid\",\"icon_url\":null,\"local_id\":4,\"voice_samples\":[]},{\"name\":\"Shout\",\"icon_url\":null,\"local_id\":5,\"voice_samples\":[]},{\"name\":\"Surprise\",\"icon_url\":null,\"local_id\":6,\"voice_samples\":[]}]}],\"created_at\":\"2025-04-02T00:53:01.440467+09:00\",\"updated_at\":\"2025-07-24T13:31:29.395713+09:00\"}",
            ];

        public static AivisSpeechCloudAPIModelInfo[] GetDefaultModels()
        {
            return [.. DefaultModels.Select(Json.Json.LoadFromText<AivisSpeechCloudAPIModelInfo>).OfType<AivisSpeechCloudAPIModelInfo>()];
        }
    }
}