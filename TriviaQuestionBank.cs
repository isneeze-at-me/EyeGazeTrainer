using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace EyeGazeApiViability
{
    internal sealed class TriviaQuestion
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Statement { get; set; }
        public bool IsTrue { get; set; }
        public string Explanation { get; set; }
        public string FactGroup { get; set; }
    }

    /// <summary>
    /// Each of the 500 unique statements is dealt once before the deck is
    /// replenished. Dealt IDs survive app restart. Opposite versions of one
    /// fact are never shown in the same ten-question round.
    /// </summary>
    internal sealed class TriviaQuestionBank
    {
        private const string UsedKey = "EyeGazeTrainer.Trivia.UsedIds.V2";
        private readonly Random random = new Random();
        private readonly List<TriviaQuestion> questions = new List<TriviaQuestion>();
        private readonly HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> roundGroups = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> roundCategories = new HashSet<string>(StringComparer.Ordinal);
        private int roundTrue;
        private int roundFalse;
        private bool loaded;

        public async Task LoadAsync()
        {
            if (loaded) return;
            var loadedQuestions = new List<TriviaQuestion>();
            for (int first = 1; first <= 500; first += 100)
            {
                string path = string.Format("ms-appx:///Trivia/true-false-{0:000}-{1:000}.json",
                                            first, first + 99);
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(path));
                string json = await FileIO.ReadTextAsync(file);
                JsonArray array = JsonArray.Parse(json);
                foreach (IJsonValue item in array)
                {
                    JsonObject o = item.GetObject();
                    loadedQuestions.Add(new TriviaQuestion
                    {
                        Id = o.GetNamedString("id"),
                        Category = o.GetNamedString("category"),
                        Statement = o.GetNamedString("question"),
                        IsTrue = o.GetNamedBoolean("answer"),
                        Explanation = o.GetNamedString("explanation"),
                        FactGroup = o.GetNamedString("factGroup")
                    });
                }
            }
            if (loadedQuestions.Count != 500 ||
                loadedQuestions.Select(q => q.Id).Distinct().Count() != 500)
                throw new InvalidOperationException("The trivia question bank is incomplete.");

            questions.AddRange(loadedQuestions);
            try
            {
                object saved;
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(UsedKey, out saved))
                {
                    foreach (string id in Convert.ToString(saved).Split(','))
                        if (!string.IsNullOrEmpty(id)) usedIds.Add(id);
                }
            }
            catch (Exception) { /* Fresh deck if settings unavailable. */ }
            usedIds.RemoveWhere(id => !questions.Any(q => q.Id == id));
            loaded = true;
        }

        public void BeginRound()
        {
            roundGroups.Clear();
            roundCategories.Clear();
            roundTrue = 0;
            roundFalse = 0;
        }

        public TriviaQuestion Draw()
        {
            if (!loaded) throw new InvalidOperationException("Load trivia before drawing.");
            var candidates = questions.Where(q => !usedIds.Contains(q.Id) &&
                                                  !roundGroups.Contains(q.FactGroup)).ToList();
            if (candidates.Count == 0)
            {
                // Near the end of a deck, preserving the no-repeat guarantee
                // takes priority over avoiding a second version of a fact.
                candidates = questions.Where(q => !usedIds.Contains(q.Id)).ToList();
            }
            if (candidates.Count == 0)
            {
                // Only replenish after ALL 500 question IDs have been dealt.
                usedIds.Clear();
                candidates = questions.Where(q => !roundGroups.Contains(q.FactGroup)).ToList();
            }
            if (candidates.Count == 0)
                throw new InvalidOperationException("No distinct trivia facts are available.");

            // Prefer a new topic and a balanced answer mix when possible.
            var topics = candidates.Where(q => !roundCategories.Contains(q.Category)).ToList();
            if (topics.Count > 0) candidates = topics;
            bool preferTrue = roundTrue < roundFalse ? true :
                              roundFalse < roundTrue ? false : random.Next(2) == 0;
            var balanced = candidates.Where(q => q.IsTrue == preferTrue).ToList();
            if (balanced.Count > 0) candidates = balanced;
            TriviaQuestion selected = candidates[random.Next(candidates.Count)];
            usedIds.Add(selected.Id);
            roundGroups.Add(selected.FactGroup);
            roundCategories.Add(selected.Category);
            if (selected.IsTrue) roundTrue++; else roundFalse++;
            try
            {
                ApplicationData.Current.LocalSettings.Values[UsedKey] =
                    string.Join(",", usedIds.OrderBy(s => s));
            }
            catch (Exception) { /* Quiz remains playable without persistence. */ }
            return selected;
        }
    }
}
