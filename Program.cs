using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;

namespace wordle
{
    class Program
    {

        private static readonly HttpClient httpClient = new HttpClient();

        static List<char> alphabet = new List<char>()
        {
            'a','b','c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
        };

        static string serverUri = "https://wordle.panaxeo.com";

        static bool isTest = false;

        static async Task Main(string[] args)
        {
            var wordleLength = 5;
            var scoreToWin = 3.507;

            var bestScore = Double.Parse(System.IO.File.ReadAllText($"best_score_{wordleLength}.txt"));
            
            var results = new Queue<double>(
                System.IO.File
                    .ReadAllText($"score_{wordleLength}.txt")
                    .Split(';')
                    .Select(value => Double.Parse(value))
            );

            var resultsSum = results.Sum();
            var currentScore = resultsSum / results.Count;
            var count = 1;

            Console.WriteLine("Press ESC to stop");

            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                && (currentScore >= scoreToWin || results.Count != 256))
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                
                var newResult = Convert.ToDouble(
                    await Run(wordleLength)
                );

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                
                if (results.Count == 256)
                {
                    var obsoleteResult = results.Dequeue();
                    resultsSum -= obsoleteResult;
                }

                results.Enqueue(newResult);
                resultsSum += newResult;
                currentScore = resultsSum / results.Count;
                Console.WriteLine($"Guess {count} (runtime {elapsedTime}): {newResult} -> currentScore: {currentScore}");
                count++;
            }
            if (!isTest)
            {
                if (currentScore < bestScore)
                    System.IO.File.WriteAllText($"best_score_{wordleLength}.txt", currentScore.ToString());

                System.IO.File.WriteAllText($"score_{wordleLength}.txt", String.Join(';', results));
            }

        }

        // static void Register()
        // {
        //     var response = await httpClient.GetAsync("https://wordle.panaxeo.com/register/michal8/michal.koplinger@panaxeo.com/");
        //     var responseBody = await response.Content.ReadAsStringAsync();
        // }

        static async Task<int> Run(int wordleLength)
        {
            var token = "3a4a71bc-29c8-4de4-81d6-c811bd8c3b1e";
            List<string> allWords = new List<string>();
            List<string> words;
            string[] responses = new string[] {};
            var letters = new HashSet<char>(alphabet);
            var count = 0;
            var response = "";
            var path = "";
            var tmpResult = new Dictionary<int, char>();
            List<char> guess = new List<char>();
            var gameid = "";
            var lettersToTry = new Dictionary<char, HashSet<int>>();
            var lines = System.IO.File.ReadLines($"./all_results_{wordleLength}.txt").Select(l => l.Split(';')).ToDictionary(parts => parts[0], parts => Int32.Parse(parts[1]));

            if (!isTest)
            {
                var responseA = await httpClient.GetAsync($"https://wordle.panaxeo.com/start/{token}/{wordleLength}/");
                var responseBodyA = await responseA.Content.ReadAsStringAsync();
                var responseDTO = JsonSerializer.Deserialize<StartGameResponse>(responseBodyA);
                gameid = responseDTO.gameid;
                words = responseDTO.candidate_solutions.ToList();
                allWords = responseDTO.candidate_solutions.ToList();
            }
            else
            {
                words = System.IO.File.ReadAllText($"test.txt").Split(';').ToList();
                //responses = new string[] {"N_NN", "NNNY", "", ""};
                responses = new string[] {"N_NN", "NNNY", "NYNN", "N_NN", "NNYN", "NYYY", "NYYY", "YYYY"};
            }
            
            var winCondition = wordleLength switch
            {
                4 => "YYYY",
                5 => "YYYYY",
                6 => "YYYYYY",
                _ => throw new ArgumentOutOfRangeException()
            };

            while (response != winCondition)
            {
                if (words.Count == 1)
                    guess = words[0].ToList();
                else
                    guess = Guess(wordleLength, words, letters, tmpResult, lettersToTry, lines);

                if (!isTest)
                {
                    var httpResponse = await httpClient.GetAsync($"{serverUri}/guess/{gameid}/{String.Concat(guess)}/");
                    var responseBody = JsonSerializer.Deserialize<ResultResponse>(await httpResponse.Content.ReadAsStringAsync());
                    response = responseBody.result;
                }
                else
                {
                    response = responses[count];
                }
                var message = $"{String.Concat(guess)} ({response};{words.Count};{letters.Count}) -> ";
                // Console.WriteLine(message);
                path += message;

                (tmpResult, words, letters, lettersToTry) = GetResult(response, words, guess, letters, tmpResult, lettersToTry);
                count++;
            }

            if (!isTest)
            {
                if ((wordleLength == 4 && count > 6) || (wordleLength >= 5 && count > 5))
                    System.IO.File.WriteAllText($"./words{wordleLength}/{String.Concat(guess)}_{count}.txt", $"{path}\n{String.Join(';', allWords)}");

                // var lines = System.IO.File.ReadLines($"./all_words_{wordleLength}.txt").Select(l => l.Split(';')).ToDictionary(parts => parts[0], parts => Int32.Parse(parts[1]));
                // foreach (var w in allWords)
                // {
                //     if (lines.ContainsKey(w))
                //         lines[w]++;
                //     else
                //         lines.Add(w, 1);
                // }

                // System.IO.File.WriteAllText($"./all_words_{wordleLength}.txt", String.Join('\n', lines.Select(l => $"{l.Key};{l.Value}")));


                var a = String.Concat(guess);

                if (lines.ContainsKey(a))
                    lines[a]++;
                else
                    lines.Add(a, 1);

                // if (lines[a] > 1)
                //     Console.WriteLine($"{a}: {lines[a]}");

                System.IO.File.WriteAllText($"./all_results_{wordleLength}.txt", String.Join('\n', lines.Select(l => $"{l.Key};{l.Value}")));
            }

            return count;
        }

        static (
            Dictionary<char, double> letterOccurrence,
            Dictionary<char, Dictionary<int, double>> letterOccurrenceAtPosition,
            Dictionary<char, double> multipleLetterOccourence
            ) GetLettersOccourrence(
            int wordleLength,
            List<string> words,
            HashSet<char> letters,
            Dictionary<int, char> tmpResult,
            Dictionary<char, HashSet<int>> lettersToTry,
            bool yes)
        {
            var letterOccurrence = new Dictionary<char, double>();
            var letterOccurrenceAtPosition = new Dictionary<char, Dictionary<int, double>>();
            var multipleLetterOccourence = new Dictionary<char, double>();

            foreach (var word in words)
            {
                var groups = word.GroupBy(c => c);

                foreach(var group in groups)
                {
                    if (!multipleLetterOccourence.ContainsKey(group.Key))
                        multipleLetterOccourence.Add(group.Key, 0.0);

                    if (group.Count() > 1)
                        multipleLetterOccourence[group.Key]++;
                }
            }

            var missingLetters = new HashSet<char>();

            foreach (var letter in letters)
            {
                var present = false;
                var result = 0.0;
                var dict = new Dictionary<int, double>();

                foreach (var word in words)
                {
                    for (int i = 0; i < wordleLength; i++)
                    {
                        if (!dict.ContainsKey(i))
                            dict.Add(i, 0);

                        if (word[i] == letter)
                        {
                            present = true;
                            dict[i] += 1;

                            // TODO: maybe lettersToTry same as tmpResult
                            if (!tmpResult.ContainsKey(i) || tmpResult[i] != letter)
                                result += 1;
                        }

                        // TODO: comment
                        if (dict[i] == words.Count && !tmpResult.ContainsKey(i))
                        {
                            tmpResult.Add(i, letter);

                            foreach (var l in lettersToTry)
                                lettersToTry[l.Key].Remove(i);
                        }
                    }
                }

                if (!present)
                    missingLetters.Add(letter);

                letterOccurrence.Add(letter, result);
                letterOccurrenceAtPosition.Add(letter, dict);
            }

            if (yes)
            {
                foreach (var letter in missingLetters)
                    letters.Remove(letter);
            }

            return (letterOccurrence, letterOccurrenceAtPosition, multipleLetterOccourence);
        }


        static List<char> Guess(
            int wordleLength,
            List<string> words,
            HashSet<char> letters,
            Dictionary<int, char> tmpResult,
            Dictionary<char, HashSet<int>> lettersToTry,
            Dictionary<string, int> lines)
        {
            (var letterOccurrence, var letterOccurrenceAtPosition, var multipleLetterOccourence) = GetLettersOccourrence(wordleLength, words, letters, tmpResult, lettersToTry, true);

            if (words.Count == 1)
                return words[0].ToList();
            
            if (tmpResult.Count == wordleLength)
                return tmpResult.Select(l => l.Value).ToList();


            Dictionary<int, char> bestLettersToTry = new Dictionary<int, char>();

            var freePositions = new HashSet<int>();

            for (var i = 0; i < wordleLength; i++)
            {
                if (!tmpResult.ContainsKey(i))
                    freePositions.Add(i);
            }

            var a = lettersToTry.Where(l => !tmpResult.Any(r => r.Value == l.Key)).ToDictionary(
                keySelector: l => l.Key,
                elementSelector: l => l.Value
            );
            
            if (a.Count > 0)
            {
                bestLettersToTry = GetBestLettersToTry(0, 0.0, wordleLength, a.Keys.ToList(), a, freePositions, new Dictionary<int, char>(), new HashSet<char>(), new Dictionary<int, char>(), letterOccurrenceAtPosition)
                                    .bestGuess;
            }

            // var bestGuess = Test(
            //     0,
            //     wordleLength,
            //     letterOccurrenceAtPosition,
            //     words,
            //     letters,
            //     tmpResult,
            //     lettersToTry,
            //     new List<char>()
            // );

            var bestGuess = GetBestGuess(
                0, wordleLength, 0.0, letters,
                new List<char>(),
                new List<char>(),
                new Dictionary<int, char>(),
                letterOccurrenceAtPosition,
                bestLettersToTry,
                words,
                tmpResult,
                multipleLetterOccourence,
                lines).bestGuess;


            if (words.Count > 3)
            {
                for (int i = 0; i < wordleLength; i++)
                {
                    if (
                        // letterOccurrence.Count - tmpResult.Count >= tmpResult.Count && 
                        tmpResult.ContainsKey(i)
                    && tmpResult[i] == bestGuess[i])
                    {
                        var freeLetters = letterOccurrence.Where(letter => 
                            !bestGuess.Contains(letter.Key) && !tmpResult.Any(l => l.Value == letter.Key) &&
                            (!lettersToTry.ContainsKey(letter.Key) || lettersToTry[letter.Key].Contains(i)));
                            //  || (tmpResult.ContainsKey(bestGuess.IndexOf(letter.Key)) && tmpResult[bestGuess.IndexOf(letter.Key)] == letter.Key)) 

                        if (freeLetters.Count() > 0)
                        {
                            bestGuess[i] = freeLetters.Aggregate((a, b) => {
                                if (tmpResult[i] == a.Key)
                                    return b;
                                if (tmpResult[i] == b.Key)
                                    return a;

                                return a.Value > b.Value ? a : b;
                            }).Key;

                            letterOccurrence.Remove(bestGuess[i]);
                        }
                    }
                }
            }

            return bestGuess;
        }

        static List<char> Test(
            int depth,
            int wordleLength,
            Dictionary<char, Dictionary<int, double>> letterOccurrenceAtPosition,
            List<string> words,
            HashSet<char> letters,
            Dictionary<int, char> tmpResult,
            Dictionary<char, HashSet<int>> lettersToTry,
            List<char> bestGuess
        )
        {
            if (words.Count < 2)
                return words[0].ToList();

            if (depth == wordleLength)
                return bestGuess;

            var max = letterOccurrenceAtPosition.Aggregate((a, b) => a.Value[depth] >= b.Value[depth] ? a : b);

            bestGuess.Add(max.Key);

            var newWords = max.Value[depth] == words.Count ? words : words.Where(w => w[depth] != max.Key).ToList();

            if (newWords.Count == 0)
                Console.WriteLine("");

            var newLetterOccurrenceAtPosition = GetLettersOccourrence(wordleLength, newWords, letters, tmpResult, lettersToTry, false).letterOccurrenceAtPosition;

            return Test(depth + 1, wordleLength, newLetterOccurrenceAtPosition, newWords, letters, tmpResult, lettersToTry, bestGuess);

        }

        static (double bestScore, List<char> bestGuess) GetBestGuess(
            int depth,
            int wordleLength,
            double bestScore,
            HashSet<char> letters,
            List<char> tmpGuess,
            List<char> bestGuess,
            Dictionary<int, char> tmpLetterByPosition,
            Dictionary<char, Dictionary<int, double>> letterOccurrenceAtPosition,
            Dictionary<int, char> lettersToTry,
            List<string> words,
            Dictionary<int, char> tmpResult,
            Dictionary<char, double> multipleLetterOccourence,
            Dictionary<string, int> lines)
        {
            if (depth == wordleLength)
            {
                var penalty = tmpGuess
                    .GroupBy(c => c)
                    .ToDictionary(keySelector: group => group.Key, elementSelector: group => {
                        // var inResultGroups = tmpResult.Select(x => x.Value).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

                        // if (group.Count() > 1 && inResultGroups.ContainsKey(group.Key))
                        // {
                        //     var count = group.Count() - inResultGroups[group.Key];
                            
                        //     return count > 1 ? count : 1; 
                        // }

                        return group.Count();
                });

                var tmpScore = 0.0;
        
                for (int i = 0; i < wordleLength; i++)
                {
                    var letter = tmpLetterByPosition[i];
                    var penaltyForLetter = penalty[letter] > 1 
                                        && (!tmpResult.ContainsKey(i) || tmpResult[i] != letter)
                                        && wordleLength - tmpResult.Count > 1 ?
                         (multipleLetterOccourence[letter] / words.Count) : 1;
                         
                    tmpScore += letterOccurrenceAtPosition[letter][i] * penaltyForLetter;
                }


                if (wordleLength - tmpResult.Count <= 1 || words.Count <= 3)
                {
                    var wordIndex = words.IndexOf(String.Concat(tmpGuess));

                    if (wordIndex != -1)
                    {
                        var p = lines.ContainsKey(words[wordIndex]) ? lines[words[wordIndex]] + 1 : 1;
                        tmpScore += 1.0 / p;
                    }
                }

                return (tmpScore, tmpGuess);
            }

            foreach (var letter in letters)
            {
                if (
                    (lettersToTry.ContainsKey(depth) && lettersToTry[depth] != '0' && lettersToTry[depth] != letter)
                    || tmpResult.ContainsKey(depth) && tmpResult[depth] != letter)
                    continue;

                if (!tmpLetterByPosition.ContainsKey(depth))
                    tmpLetterByPosition.Add(depth, letter);
                else
                    tmpLetterByPosition[depth] = letter;

                if (tmpGuess.Count == depth)
                    tmpGuess.Add(letter);
                else
                    tmpGuess[depth] = letter;

                var result = GetBestGuess(depth + 1, wordleLength, bestScore, letters, tmpGuess, bestGuess, tmpLetterByPosition, letterOccurrenceAtPosition, lettersToTry, words, tmpResult, multipleLetterOccourence, lines);

                if (result.bestScore > bestScore)
                {
                    bestScore = result.bestScore;
                    bestGuess = new List<char>(result.bestGuess);
                }
            }

            return (bestScore, bestGuess);
        }

        static (double bestScore, Dictionary<int, char> bestGuess) GetBestLettersToTry(
            int depth,
            double bestScore,
            int wordleLength,
            List<char> lettersToTryList,
            Dictionary<char, HashSet<int>> lettersToTry,
            HashSet<int> freePositions,
            Dictionary<int, char> tmpGuess,
            HashSet<char> tmpGuessSet,
            Dictionary<int, char> bestGuess,
            Dictionary<char, Dictionary<int, double>> letterOccurrenceAtPosition)
        {
            if (!freePositions.Contains(depth))
                depth++;

            if (depth >= wordleLength)
            {
                var tmpScore = 0.0;

                foreach (var letter in tmpGuess) {
                    if (letter.Value == '0')
                        continue;

                    tmpScore += letterOccurrenceAtPosition[letter.Value][letter.Key];
                }

                return (tmpScore, tmpGuess);
            }

            char? previousLetterToTry = null;

            for (int i = 0; i < freePositions.Count * lettersToTry.Count; i++)
            {
                char letterToTry;
                
                if (lettersToTryList.Count <= i)
                {
                    letterToTry = '0';
                }
                else
                {
                    letterToTry = lettersToTryList[i];

                    if (tmpGuessSet.Contains(letterToTry))
                        continue;

                    if (!lettersToTry[letterToTry].Contains(depth)) // TODO: depth ??
                        continue;
                }

                if (previousLetterToTry == letterToTry)
                    continue;

                tmpGuessSet.Add(letterToTry);
                tmpGuess.Add(depth, letterToTry);

                var result = GetBestLettersToTry(depth + 1, bestScore, wordleLength, lettersToTryList, lettersToTry, freePositions, tmpGuess, tmpGuessSet, bestGuess, letterOccurrenceAtPosition);
            
                if (result.bestScore > bestScore)
                {
                    bestScore = result.bestScore;
                    bestGuess = new Dictionary<int, char>(result.bestGuess);
                }

                previousLetterToTry = letterToTry;

                tmpGuessSet.Remove(letterToTry);
                tmpGuess.Remove(depth);
            }

            return (bestScore, bestGuess);
        }

        static (Dictionary<int, char>, List<string>, HashSet<char>, Dictionary<char, HashSet<int>> lettersToTry) GetResult(
            string response,
            List<string> words,
            List<char> guess,
            HashSet<char> letters,
            Dictionary<int, char> tmpResult,
            Dictionary<char, HashSet<int>> lettersToTry)
        {
            for (int i = 0; i < response.Length; i++)
            {
                if (response[i] == 'Y')
                {
                    words = words.Where((word) => word[i] == guess[i]).ToList();
                    if (words.Count == 0)
                        Console.WriteLine("");

                    if (!tmpResult.ContainsKey(i))
                        tmpResult.Add(i, guess[i]);
                }
                else if (response[i] == '_')
                {
                    words = words.Where((word) => word.IndexOf(guess[i]) != -1 && word[i] != guess[i]).ToList();
                    if (words.Count == 0)
                        Console.WriteLine("");
                    
                    if (!lettersToTry.ContainsKey(guess[i]))
                    {
                        lettersToTry.Add(guess[i], new HashSet<int>());

                        for (int j = 0; j < response.Length; j++)
                        {
                            if (!tmpResult.ContainsKey(j))
                                lettersToTry[guess[i]].Add(j);
                        }
                    }

                    lettersToTry[guess[i]].Remove(i);
                }
                else
                {
                    words = words.Where((word) => word.IndexOf(guess[i]) == -1).ToList();
                    if (words.Count == 0)
                        Console.WriteLine("");
                    letters.Remove(guess[i]);
                }
            }

            foreach (var r in tmpResult)
            {
                foreach (var l in lettersToTry)
                {
                    lettersToTry[l.Key].Remove(r.Key);

                    if (lettersToTry[l.Key].Count == 0 && letters.Contains(l.Key) && !tmpResult.Any(x => x.Value == l.Key))
                    {
                        letters.Remove(l.Key);
                        words = words.Where((word) => word.IndexOf(l.Key) == -1).ToList();
                        if (words.Count == 0)
                            Console.WriteLine("");
                        lettersToTry.Remove(l.Key);
                    }
                }
            }

            foreach (var l in lettersToTry)
            {
                if (l.Value.Count == 1 && !tmpResult.Any(r => r.Value == l.Key))
                {
                    tmpResult[l.Value.First()] = l.Key;
                    words = words.Where((word) => word[l.Value.First()] == l.Key).ToList();
                    lettersToTry[l.Key].Remove(l.Value.First());
                    
                    if (words.Count == 0)
                        Console.WriteLine("test");
                }
            }

            return (tmpResult, words, letters, lettersToTry);
        }
    }

    public class StartGameResponse
    {
        public string[] candidate_solutions { get; set; }
        public string gameid { get; set; }
    }

    public class ResultResponse
    {
        public string result { get; set; }

    }
}
