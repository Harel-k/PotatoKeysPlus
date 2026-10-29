using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Typing Trainer: measures your real typing speed as you use the PC (only counts keys,
/// never stores what you type), and makes typing tests that focus on your weak letters.
/// </summary>
public sealed class TypingTrainerModule(TypingSettings settings, Action save)
{
    // A pause longer than this ends a typing "burst" and isn't counted as typing time.
    const double PauseSeconds = 2.0;

    readonly Queue<DateTime> _recent = new();
    DateTime _last = DateTime.MinValue;
    DateTime _burstStart;
    long _burstChars;

    public TypingSettings Settings => settings;

    public void OnKeyDown(int vk, Mods mods, bool blocked)
    {
        if (!settings.Enabled || blocked || (mods & (Mods.Ctrl | Mods.Alt | Mods.Win)) != 0) return;
        var day = Today();
        day.Keys++;
        if (vk == 0x08) { day.Backspaces++; save(); return; }
        if (!IsTypingKey(vk)) return;

        var now = DateTime.Now;
        double gap = (now - _last).TotalSeconds;
        if (gap > PauseSeconds)
        {
            _burstStart = now;
            _burstChars = 0;
        }
        else
        {
            day.ActiveSeconds += gap;
        }
        _last = now;
        day.Chars++;
        _burstChars++;
        _recent.Enqueue(now);
        while (_recent.Count > 0 && (now - _recent.Peek()).TotalSeconds > 15) _recent.Dequeue();

        // A burst only counts as a "best" once it's long enough to mean something.
        double burstSeconds = (now - _burstStart).TotalSeconds;
        if (burstSeconds >= 10)
        {
            double wpm = _burstChars / 5.0 / (burstSeconds / 60);
            if (wpm < 250)
            {
                day.BestBurstWpm = Math.Max(day.BestBurstWpm, wpm);
                settings.BestBurstWpm = Math.Max(settings.BestBurstWpm, wpm);
            }
        }
        save();
    }

    /// <summary>Speed over the last 15 seconds (0 when you're not typing).</summary>
    public double LiveWpm
    {
        get
        {
            var now = DateTime.Now;
            if ((now - _last).TotalSeconds > PauseSeconds || _recent.Count < 5) return 0;
            double span = Math.Max(3, (now - _recent.Peek()).TotalSeconds);
            return _recent.Count / 5.0 / (span / 60);
        }
    }

    public TypingDay Today() => Day(DateTime.Today);

    public TypingDay Day(DateTime date)
    {
        var key = date.ToString("yyyy-MM-dd");
        if (!settings.Days.TryGetValue(key, out var day))
        {
            day = new TypingDay();
            settings.Days[key] = day;
            // Keep about two months of history.
            foreach (var old in settings.Days.Keys.Where(k => string.CompareOrdinal(k, DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd")) < 0).ToList())
                settings.Days.Remove(old);
        }
        return day;
    }

    public static double AverageWpm(TypingDay day) => day.ActiveSeconds < 5 ? 0 : day.Chars / 5.0 / (day.ActiveSeconds / 60);

    static bool IsTypingKey(int vk) =>
        vk is 0x20 or 0x0D or (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) or (>= 0x60 and <= 0x6F)
            or (>= 0xBA and <= 0xC0) or (>= 0xDB and <= 0xDF) or 0xE2;

    // ---------- Typing test ----------

    static readonly string[] Words =
    [
        "the", "be", "to", "of", "and", "a", "in", "that", "have", "it", "for", "not", "on", "with", "he", "as", "you",
        "do", "at", "this", "but", "his", "by", "from", "they", "we", "say", "her", "she", "or", "an", "will", "my",
        "one", "all", "would", "there", "their", "what", "so", "up", "out", "if", "about", "who", "get", "which", "go",
        "me", "when", "make", "can", "like", "time", "no", "just", "him", "know", "take", "people", "into", "year",
        "your", "good", "some", "could", "them", "see", "other", "than", "then", "now", "look", "only", "come", "its",
        "over", "think", "also", "back", "after", "use", "two", "how", "our", "work", "first", "well", "way", "even",
        "new", "want", "because", "any", "these", "give", "day", "most", "us", "is", "was", "are", "been", "has",
        "had", "were", "said", "did", "having", "may", "should", "call", "world", "school", "still", "try", "last",
        "ask", "need", "too", "feel", "three", "state", "never", "become", "between", "high", "really", "something",
        "another", "family", "own", "leave", "put", "old", "while", "mean", "keep", "student", "why", "let", "great",
        "same", "big", "group", "begin", "seem", "country", "help", "talk", "where", "turn", "problem", "every",
        "start", "hand", "might", "show", "part", "against", "place", "such", "again", "few", "case", "week",
        "company", "system", "each", "right", "program", "hear", "question", "during", "play", "government", "run",
        "small", "number", "off", "always", "move", "night", "live", "point", "believe", "hold", "today", "bring",
        "happen", "next", "without", "before", "large", "million", "must", "home", "under", "water", "room", "write",
        "mother", "area", "national", "money", "story", "young", "fact", "month", "lot", "study", "book", "eye",
        "job", "word", "business", "issue", "side", "kind", "four", "head", "far", "black", "long", "both", "little",
        "house", "yes", "since", "provide", "service", "around", "friend", "important", "father", "sit", "away",
        "until", "power", "hour", "game", "often", "yet", "line", "end", "among", "ever", "stand", "bad", "lose",
        "however", "member", "pay", "law", "meet", "car", "city", "almost", "include", "continue", "set", "later",
        "community", "much", "name", "five", "once", "white", "least", "president", "learn", "real", "change", "team",
        "minute", "best", "several", "idea", "kid", "body", "information", "nothing", "ago", "lead", "social",
        "understand", "whether", "watch", "together", "follow", "parent", "stop", "face", "anything", "create",
        "public", "already", "speak", "others", "read", "level", "allow", "add", "office", "spend", "door", "health",
        "person", "art", "sure", "war", "history", "party", "within", "grow", "result", "open", "morning", "walk",
        "reason", "low", "win", "research", "girl", "guy", "early", "food", "moment", "himself", "air", "teacher",
        "force", "offer", "enough", "education", "across", "although", "remember", "foot", "second", "boy", "maybe",
        "toward", "able", "age", "policy", "everything", "love", "process", "music", "including", "consider",
        "appear", "actually", "buy", "probably", "human", "wait", "serve", "market", "die", "send", "expect", "sense",
        "build", "stay", "fall", "oh", "nation", "plan", "cut", "college", "interest", "death", "course", "someone",
        "experience", "behind", "reach", "local", "kill", "six", "remain", "effect", "yeah", "suggest", "class",
        "control", "raise", "care", "perhaps", "late", "hard", "field", "else", "pass", "former", "sell", "major",
        "sometimes", "require", "along", "development", "themselves", "report", "role", "better", "economic",
        "effort", "decide", "rate", "strong", "possible", "heart", "drug", "show", "leader", "light", "voice", "wife",
        "whole", "police", "mind", "finally", "pull", "return", "free", "military", "price", "less", "according",
        "decision", "explain", "son", "hope", "develop", "view", "relationship", "carry", "town", "road", "drive",
        "arm", "true", "federal", "break", "difference", "thank", "receive", "value", "international", "building",
        "action", "full", "model", "join", "season", "society", "tax", "director", "position", "player", "agree",
        "especially", "record", "pick", "wear", "paper", "special", "space", "ground", "form", "support", "event",
        "official", "whose", "matter", "everyone", "center", "couple", "site", "project", "hit", "base", "activity",
        "star", "table", "court", "produce", "eat", "american", "teach", "oil", "half", "situation", "easy", "cost",
        "industry", "figure", "street", "image", "itself", "phone", "either", "data", "cover", "quite", "picture",
        "clear", "practice", "piece", "land", "recent", "describe", "product", "doctor", "wall", "patient", "worker",
        "news", "test", "movie", "certain", "north", "personal", "simply", "third", "technology", "catch", "step",
        "baby", "computer", "type", "attention", "draw", "film", "tree", "source", "red", "nearly", "organization",
        "choose", "cause", "hair", "century", "evidence", "window", "difficult", "listen", "soon", "culture",
        "billion", "chance", "brother", "energy", "period", "summer", "realize", "hundred", "available", "plant",
        "likely", "opportunity", "term", "short", "letter", "condition", "choice", "single", "rule", "daughter",
        "administration", "south", "husband", "floor", "campaign", "material", "population", "economy", "medical",
        "hospital", "church", "close", "thousand", "risk", "current", "fire", "future", "wrong", "involve", "defense",
        "anyone", "increase", "security", "bank", "myself", "certainly", "west", "sport", "board", "seek", "per",
        "subject", "officer", "private", "rest", "behavior", "deal", "performance", "fight", "throw", "top", "quickly",
        "past", "goal", "bed", "order", "author", "fill", "represent", "focus", "foreign", "drop", "blood", "upon",
        "agency", "push", "nature", "color", "recently", "store", "reduce", "sound", "note", "fine", "near",
        "movement", "page", "enter", "share", "common", "poor", "natural", "race", "concern", "series", "significant",
        "similar", "hot", "language", "each", "usually", "response", "dead", "rise", "animal", "factor", "decade",
        "article", "shoot", "east", "save", "seven", "artist", "away", "scene", "stock", "career", "despite",
        "central", "eight", "thus", "treatment", "beyond", "happy", "exactly", "protect", "approach", "lie", "size",
        "dog", "fund", "serious", "occur", "media", "ready", "sign", "thought", "list", "individual", "simple",
        "quality", "pressure", "accept", "answer", "resource", "identify", "left", "meeting", "determine", "prepare",
        "disease", "whatever", "success", "argue", "cup", "particularly", "amount", "ability", "staff", "recognize",
        "indicate", "character", "growth", "loss", "degree", "wonder", "attack", "herself", "region", "television",
        "box", "training", "pretty", "trade", "election", "everybody", "physical", "lay", "general", "feeling",
        "standard", "bill", "message", "fail", "outside", "arrive", "analysis", "benefit", "sex", "forward", "lawyer",
        "present", "section", "environmental", "glass", "skill", "sister", "professor", "operation", "financial",
        "crime", "stage", "ok", "compare", "authority", "miss", "design", "sort", "act", "ten", "knowledge", "gun",
        "station", "blue", "strategy", "clearly", "discuss", "indeed", "truth", "song", "example", "democratic",
        "check", "environment", "leg", "dark", "various", "rather", "laugh", "guess", "executive", "prove", "hang",
        "entire", "rock", "forget", "claim", "remove", "manager", "enjoy", "network", "legal", "religious", "cold",
        "final", "main", "science", "green", "memory", "card", "above", "seat", "cell", "establish", "nice", "trial",
        "expert", "spring", "firm", "radio", "visit", "management", "avoid", "imagine", "tonight", "huge", "ball",
        "finish", "yourself", "theory", "impact", "respond", "statement", "maintain", "charge", "popular",
        "traditional", "onto", "reveal", "direction", "weapon", "employee", "cultural", "contain", "peace", "pain",
        "apply", "play", "measure", "wide", "shake", "fly", "interview", "manage", "chair", "fish", "particular",
        "camera", "structure", "politics", "perform", "bit", "weight", "suddenly", "discover", "candidate",
        "production", "treat", "trip", "evening", "affect", "inside", "conference", "unit", "style", "adult",
        "worry", "range", "mention", "deep", "edge", "specific", "writer", "trouble", "necessary", "throughout",
        "challenge", "fear", "shoulder", "institution", "middle", "sea", "dream", "bar", "beautiful", "property",
        "instead", "improve", "stuff", "quiz", "jump", "jazz", "box", "fox", "zone", "quick", "zero", "quiet",
        "joke", "oxygen", "puzzle", "quest", "jacket", "galaxy", "frozen", "awkward", "pixel", "keyboard", "potato",
    ];

    /// <summary>Makes a test that leans towards words with the letters you miss most.</summary>
    public string MakeTest(int wordCount = 25)
    {
        var weak = WeakLetters(6).ToDictionary(w => w.Letter, w => w.MissRate);
        var rnd = new Random();
        var picked = new List<string>(wordCount);
        while (picked.Count < wordCount)
        {
            // Draw a few candidates and keep the one that trains weak letters the most.
            string best = Words[rnd.Next(Words.Length)];
            double bestScore = -1;
            for (int i = 0; i < 4; i++)
            {
                var word = Words[rnd.Next(Words.Length)];
                double score = rnd.NextDouble() * 0.3 + word.Sum(c => weak.TryGetValue(c, out var r) ? r * 4 : 0);
                if (score > bestScore) { best = word; bestScore = score; }
            }
            if (picked.Count == 0 || picked[^1] != best) picked.Add(best);
        }
        return string.Join(' ', picked);
    }

    public void RecordLetter(char expected, bool missed)
    {
        expected = char.ToLowerInvariant(expected);
        if (expected is < 'a' or > 'z') return;
        var key = expected.ToString();
        settings.LetterSeen[key] = settings.LetterSeen.GetValueOrDefault(key) + 1;
        if (missed) settings.LetterMissed[key] = settings.LetterMissed.GetValueOrDefault(key) + 1;
    }

    public void RecordTest(double wpm, double accuracy)
    {
        settings.Tests.Add(new TypingTestResult { When = DateTime.Now, Wpm = wpm, Accuracy = accuracy });
        if (settings.Tests.Count > 50) settings.Tests.RemoveAt(0);
        settings.BestTestWpm = Math.Max(settings.BestTestWpm, wpm);
        save();
    }

    public IEnumerable<(char Letter, double MissRate)> WeakLetters(int count) =>
        settings.LetterSeen
            .Where(kv => kv.Value >= 8)
            .Select(kv => (Letter: kv.Key[0], MissRate: settings.LetterMissed.GetValueOrDefault(kv.Key) / (double)kv.Value))
            .Where(x => x.MissRate > 0)
            .OrderByDescending(x => x.MissRate)
            .Take(count);
}
