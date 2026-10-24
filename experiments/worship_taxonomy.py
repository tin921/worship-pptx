"""Human-written hypotheses for corpus screening, not model-generated truth.

The 80 scene candidates are frozen before reading holdout results. The 60-scene
subset retains the original 31 and adds the 29 with most discovery-set literal
support. Spiritual subject labels are a separate, multi-label vocabulary.
"""
from image_keywords import SCENES as ORIGINAL_SCENES

# id, Traditional Chinese display label, embedding description, English search
# phrase, literal Chinese imagery cues. Cues are simplified by the evaluator.
ADDITIONAL_SCENES = [
    ('bible', '聖經與書卷', '敞开的圣经书卷，神的话语真理照亮生命，阅读默想经文', 'open Bible soft light', '圣经 经文 话语 真理 律法 书卷'),
    ('communion', '聖餐餅杯', '桌上的圣餐饼与葡萄酒杯，记念耶稣舍身流血，立下新约', 'communion bread wine cup', '圣餐 擘饼 杯 新约'),
    ('nativity', '馬槽與伯利恆', '伯利恒夜晚的马槽，婴孩耶稣降生，牧人来到圣婴面前', 'Bethlehem nativity manger night', '马槽 伯利恒 圣婴 降生 圣诞'),
    ('empty_tomb', '空墓與復活晨光', '清晨空的坟墓，墓门石头滚开，耶稣复活战胜死亡', 'empty tomb resurrection dawn', '空坟 空墓 坟墓 复活 墓门'),
    ('crown', '冠冕', '荣耀的金色冠冕，君王的尊贵与得胜，生命的冠冕', 'gold crown dark background', '冠冕 冠冕 王冠'),
    ('throne', '寶座與榮光', '宏伟荣耀的宝座，万王之王掌权，威严圣洁的光辉', 'royal throne glorious light', '宝座 王位'),
    ('altar', '祭壇與獻祭', '石头祭坛上的祭物，向神献上自己成为圣洁活祭', 'stone altar warm light', '祭坛 活祭 献祭 祭物'),
    ('incense', '香煙與香爐', '香炉与升起的馨香烟雾，祷告如香升到神面前', 'incense smoke soft light', '馨香 香炉 香气 香烟'),
    ('trumpet', '號角', '金色号角吹响，宣告得胜与君王来临，众人聚集', 'trumpet shofar light', '号角 吹角 号筒 号声'),
    ('harp', '琴弦與樂器', '琴弦与竖琴在光中，以乐器弹奏称颂神', 'harp strings gentle light', '琴弦 弹琴 竖琴 琴瑟'),
    ('banner', '旌旗', '高举的旗帜和旌旗，在风中飘扬宣告得胜', 'banner flag wind sky', '旌旗 旗帜 旗号'),
    ('armor', '盔甲與寶劍', '穿戴全副军装，佩戴头盔拿起宝剑，在争战中站立', 'ancient armor sword shield', '军装 盔甲 头盔 宝剑 利剑'),
    ('shield', '盾牌', '坚固盾牌抵挡攻击，保护信靠的人免于危险', 'shield protection light', '盾牌'),
    ('broken_chains', '斷開的鎖鏈', '铁链锁链断开，牢门打开，囚禁的人得释放和自由', 'broken chains freedom light', '锁链 铁链 捆锁 囚牢 囚禁'),
    ('tent', '帳幕', '旷野中的帐幕与会幕，神住在人中间，停留亲近神', 'tabernacle tent desert light', '帐幕 会幕 帐棚'),
    ('refuge', '避難所與城堡', '坚固的避难所堡垒城堡，风雨中有安全保护和藏身之处', 'fortress refuge mountain', '避难所 山寨 堡垒 城堡 藏身'),
    ('sheltering_wings', '翅膀蔭下', '展开的羽翼遮盖小鸟，在翅膀荫下安歇并得到保护', 'bird sheltering wings nest', '翅膀荫 羽翼 翅膀下 翅膀底'),
    ('footprints', '沙灘腳印', '脚印一步一步留在沙滩或路上，同行陪伴跟随带领', 'footprints sand beach', '脚印 足迹 脚踪'),
    ('door', '敞開的門', '光从敞开的门透进来，开门迎接，通往新的道路', 'open door light', '开门 敞开门 门户 门口 窄门'),
    ('bridge', '橋樑', '桥梁跨越河流与隔阂，连接两岸，让人重新相遇', 'bridge river morning light', '桥梁 桥'),
    ('boat', '海上小船', '小船行过海面与风浪，耶稣同在船上，航行靠岸', 'small boat sea horizon', '船 航行 航海'),
    ('anchor', '錨', '船锚牢牢抓住海底，在风浪中坚定稳妥，灵魂的锚', 'ship anchor sea', '船锚 锚'),
    ('harvest', '成熟莊稼', '金黄成熟的庄稼和农田，工人收割，丰盛收成', 'golden harvest field', '庄稼 收割 丰收 收成'),
    ('wheat', '麥粒與麥穗', '一粒麦子落入土地，结出麦穗和许多果实，舍己与新生命', 'wheat grain ears field', '麦子 麦粒 麦穗'),
    ('grapevine', '葡萄樹與枝子', '葡萄树枝子连接树干，结果累累，常在主里面', 'grapevine branches fruit', '葡萄 枝子'),
    ('olive_tree', '橄欖樹與橄欖枝', '橄榄树和绿色橄榄枝，扎根生长，和平与忠信', 'olive tree branches sunlight', '橄榄'),
    ('tree', '溪邊大樹', '树栽在溪水旁，树根深入土地，叶子常青，成长结果', 'tree by river roots', '树根 树木 大树 生命树 树栽 结果子'),
    ('garden', '花園與果園', '宁静花园果园，花香与果实，走入园中与所爱者相遇', 'peaceful garden orchard', '花园 园中 果园 伊甸'),
    ('flowers', '花朵盛開', '鲜花盛开在阳光中，花瓣与花香，生命美丽短暂', 'flowers blooming sunlight', '花朵 花开 花瓣 玫瑰'),
    ('lily', '百合花', '山谷中纯洁的百合花，野地百合被看顾，细致美丽', 'white lily valley', '百合'),
    ('pottery', '陶匠與陶器', '陶匠的手塑造泥土和陶器，重新修补器皿，生命被塑造', 'potter hands clay vessel', '陶匠 陶器 瓦器 泥土 器皿'),
    ('dew', '露珠', '清晨露水落在叶片和花草上，晶莹滋润，安静清新', 'morning dew leaves', '露水 甘露 露珠'),
    ('river', '大河與江水', '宽阔大河江水流淌，河岸与远方，生命河流带来丰盛', 'wide river flowing water', '江河 江水 河流 生命河 河水'),
    ('desert', '曠野沙漠', '广阔旷野沙漠，干旱土地上仍然行走，在荒凉中寻找出路', 'desert wilderness dunes', '旷野 沙漠 荒漠 荒地'),
    ('sea_parting', '紅海開路', '红海分开成为干地，道路穿过海水，两侧水墙，出埃及得拯救', 'Red Sea parted path', '红海 分开海 分海 海中开路'),
    ('new_jerusalem', '新耶路撒冷', '光明圣城新耶路撒冷，珍珠城门和金街，神与人永远同住', 'new Jerusalem golden city', '新耶路撒冷 圣城 金街 珍珠门'),
    ('city', '城市與家園', '城市街道和家乡，愿平安公义临到城市，关心身边的邻舍', 'city skyline peaceful light', '城市 这城 香港 街道 家乡 这地'),
    ('nations', '萬國與世界', '世界各地不同民族聚集，跨越疆界，将福音带到万国万民', 'world nations people unity', '万国 万国 万国万民 列邦 地极 万国列邦'),
    ('family', '家庭與孩子', '父母与孩子在家中相伴，家庭的爱与照顾，儿女的信任', 'family parent child warm light', '父母 孩子 家庭 儿女 母亲 父亲'),
    ('embrace', '擁抱與懷抱', '温柔拥抱疲惫忧伤的人，回到父亲怀中，被接纳与安慰', 'gentle embrace warm light', '拥抱 怀抱 怀中'),
    ('helping_neighbor', '扶助與分享', '伸手扶助有需要的人，分享食物和关怀，以行动服侍邻舍', 'helping hands sharing kindness', '邻舍 穷人 饥饿 贫穷 施舍 关怀'),
    ('tears', '淚滴與雨窗', '眼泪和窗上雨滴，伤心哀哭与思念，在忧伤中寻求安慰', 'raindrops window quiet sorrow', '眼泪 泪水 哭泣 哀哭 泪'),
    ('wedding', '婚宴與戒指', '婚宴与婚礼，爱人立约，等候新郎迎接新妇，忠贞的爱', 'wedding rings banquet light', '婚宴 婚礼 新郎 新妇 新娘'),
    ('angels', '天使與天軍', '天使和天军在光中歌唱赞美，传递佳音与荣耀', 'angels heavenly light', '天使 天军'),
    ('pillar_cloud_fire', '雲柱火柱', '旷野中的云柱火柱，白天黑夜带领前方道路，神的指引', 'pillar cloud fire wilderness', '云柱 火柱'),
    ('lamp', '燈火與燈台', '灯台灯火照亮黑暗，一盏灯照亮脚前的路', 'lamp lantern dark path', '灯台 灯火 明灯 灯盏 脚前的灯'),
    ('treasure', '珍珠與寶藏', '珍贵的珍珠宝石与宝藏，将最宝贵的献上，寻找永恒价值', 'pearl treasure gentle light', '珍珠 宝藏 财宝 宝贝'),
    ('kneeling_prayer', '屈膝禱告', '跪下祷告，俯伏在神面前，谦卑安静交托寻求神', 'kneeling prayer silhouette', '屈膝 跪下 俯伏'),
    ('bread_provision', '餅與日用飲食', '桌上的面包和粮食，饥饿的人得到供应，每日所需被满足', 'bread table warm light', '粮食 食物 日用 饮食 生命粮 五饼 二鱼'),
]

SCENES = list(ORIGINAL_SCENES) + ADDITIONAL_SCENES
assert len(SCENES) == 80, len(SCENES)
assert len({s[0] for s in SCENES}) == 80

# id, subject label, semantic description, cue phrases. These are SUBJECTS,
# not pictures: a song can have multiple subjects and several imagery options.
SUBJECTS = [
    ('praise', '敬拜讚美', '歌颂赞美神的伟大荣耀，举手敬拜称颂赞扬主，主配得尊荣', '赞美 敬拜 颂赞 歌颂 称颂 哈利路亚'),
    ('thanks', '感恩稱謝', '感谢神的恩惠与赐福，数算恩典，献上感恩称谢', '感谢 感恩 称谢 数算'),
    ('grace', '恩典憐憫', '白白领受恩典，神以怜悯接纳软弱的人，依靠恩典而活', '恩典 怜悯 恩惠 恩慈'),
    ('love', '神愛與親密', '神的慈爱永不改变，回应主的爱，与主相爱亲密相伴', '慈爱 爱我 爱你 爱主 深爱 爱慕 亲密'),
    ('cross', '十架救贖', '耶稣在十字架舍身流血，担当罪孽，以宝血赎回人', '十架 十字架 宝血 救赎 赎罪 流血'),
    ('resurrection', '復活得勝', '耶稣从死里复活战胜死亡，信徒与基督同活', '复活 空坟 死亡 毒钩'),
    ('christmas', '降生聖誕', '耶稣降生成为人，伯利恒马槽圣婴带来和平与救恩', '降生 圣诞 圣婴 马槽 伯利恒'),
    ('spirit', '聖靈充滿', '求圣灵充满浇灌更新，恩膏能力临到，随从圣灵引导', '圣灵 充满 恩膏 浇灌'),
    ('presence', '同在渴慕', '渴慕亲近神的同在，寻求主面，住在主里面', '同在 渴慕 亲近 寻求你 面前 住在'),
    ('prayer', '禱告呼求', '向神祷告呼求倾心吐意，求主垂听回应帮助', '祷告 呼求 垂听 祈求 呼喊'),
    ('faith', '信靠交託', '凭信心信靠主，在不明白之时仍相信，把生命交托给神', '信靠 相信 交托 倚靠 信心 仰望'),
    ('promise', '信實應許', '神的应许永不落空，信实不变，盟约长存', '应许 信实 盟约 永不改变 约定'),
    ('guidance', '引導跟隨', '跟随耶稣脚步，求主引导人生道路，遵行旨意', '跟随 带领 引导 脚步 道路 指引'),
    ('surrender', '奉獻委身', '将自己完全献给主，愿一生活祭委身，顺服旨意舍己', '献上 奉献 委身 活祭 舍己 顺服'),
    ('repentance', '悔改赦免', '承认过犯回转，求神赦免洁净，离弃罪恶重新归向神', '悔改 赦免 认罪 回转 罪恶 过犯'),
    ('holiness', '聖潔塑造', '求主洁净内心，塑造生命圣洁，成为合用器皿', '圣洁 洁净 塑造 陶造 器皿 炼净'),
    ('renewal', '更新生命', '在基督里成为新造的人，旧事过去，生命重新开始成长', '更新 新造 生命 改变 重生'),
    ('hope', '盼望等候', '在困境中等候盼望，黑夜将过去，相信未来有神的恩典', '盼望 等候 曙光 盼着 忍耐'),
    ('peace', '平安安息', '卸下忧虑恐惧，在神怀中安息，领受平安宁静', '平安 安息 安歇 安静 安稳 不惧怕'),
    ('comfort', '安慰醫治', '神安慰忧伤破碎的人，擦干眼泪，医治伤痛带来复原', '安慰 医治 伤痛 破碎 泪水 抚平'),
    ('lament', '苦難哀求', '在痛苦患难与失落中向神倾诉，哀哭却仍寻求神', '痛苦 苦难 哀哭 患难 黑暗 忧伤'),
    ('strength', '力量堅忍', '软弱疲乏中得到力量，靠主勇敢站立，坚持奔跑不放弃', '力量 软弱 刚强 勇敢 坚持 疲乏 不放弃'),
    ('warfare', '爭戰釋放', '靠主胜过仇敌攻击和捆绑，断开锁链，在属灵争战中得自由', '争战 仇敌 锁链 释放 自由 得胜'),
    ('sovereignty', '君王掌權', '神是万王之王坐在宝座，掌管全地，尊贵权柄荣耀归主', '掌权 宝座 君王 万王 王权 权柄'),
    ('creation', '創造萬有', '赞叹创造天地宇宙万物的神，山川海洋星辰彰显神奇妙', '创造 万物 宇宙 天地 星辰 万有'),
    ('word', '真理話語', '聆听遵行神的话语，圣经真理照亮道路，默想律法', '话语 真理 圣经 聆听 律法 经文'),
    ('provision', '供應保守', '神供应所需赐下祝福，以牧者看顾保护，作坚固保障', '供应 保守 保护 赐福 牧者 保障'),
    ('identity', '兒女歸屬', '蒙神拣选成为儿女，有尊贵身份，属于天父成为一家', '儿女 身份 拣选 属你 属于 儿子 父亲'),
    ('unity', '教會合一', '弟兄姊妹同心合一，肢体彼此相爱，建立教会和群体', '合一 同心 教会 肢体 弟兄 姊妹'),
    ('mission', '宣教差遣', '回应呼召被差遣，将福音传遍万国地极，带人归主', '福音 宣教 差遣 传扬 地极 万国 使命'),
    ('service', '愛人服侍', '爱邻舍和贫穷受苦者，彼此服侍分享，以行动彰显公义怜悯', '服侍 事奉 邻舍 贫穷 公义 关怀 分享'),
    ('revival', '復興轉化', '祈求复兴教会与世代，城市土地被更新，神国降临', '复兴 世代 城市 转化 降临 香港'),
    ('eternity', '永恆再來', '等候主再来，天国永恒荣耀，回到天家与神同住', '再来 永恒 天家 天堂 圣城 新耶路撒冷'),
    ('communion', '聖餐立約', '领受饼杯记念主身体宝血，在新约中合一领受生命', '圣餐 擘饼 饼杯 新约 记念'),
    ('joy', '喜樂歡慶', '因神欢喜快乐，高声欢呼跳舞庆祝，忧伤变为喜乐', '喜乐 欢呼 跳舞 快乐 欢欣'),
    ('family', '家庭祝福', '为婚姻家庭和孩子祝福，父母相爱扶持，世代传承信仰', '家庭 父母 婚姻 婚礼 孩子 母亲'),
]

MODIFIERS = [
    ('晨光', 'sunrise', '清晨 黎明 晨光 日出'),
    ('夜色', 'night', '黑夜 夜晚 夜间'),
    ('暖光', 'warm light', '温暖 光明 光辉 光芒'),
    ('風雨', 'storm', '风雨 风浪 暴风 波涛'),
    ('平靜', 'calm', '安静 安息 平安 安歇'),
    ('歡欣', 'joyful', '喜乐 欢欣 欢呼 快乐'),
    ('金色', 'golden', '金色 黄金 金光'),
    ('宏偉', 'majestic', '伟大 荣耀 壮丽 威严'),
]
