using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium;
using System.IO;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace SaltyBetScraper {
    class SaltyBetScraper : IDisposable {

        private IWebDriver driver;
        private DateTime? lastRequestTime = null;
        private int firstPage;
        private int lastPage;
        private StreamWriter file;
        private StreamWriter logFile;
        private Dictionary<String, String> pageErrors = new Dictionary<string, string>();
        private Dictionary<String, String> tournamentErrors = new Dictionary<string, string>();
        private Dictionary<String, String> matchErrors = new Dictionary<string, string>();

        #region Public Methods

        public SaltyBetScraper(int start, int stop) {
            firstPage = start;
            lastPage = start;

            string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).ToString();
            FirefoxDriverService service = FirefoxDriverService.CreateDefaultService(projectDirectory);
            driver = new FirefoxDriver(service);

            file = new StreamWriter("SaltyScraper.data", append: true);
            logFile = new StreamWriter("SaltyScraper.log", append: true);
        }

        public void scrapeCompendium() {

            login();

            scrapeStatsPages(firstPage, lastPage);

            driver.Close();
            driver.Dispose();

            writeLogFile();
        }

        #endregion

        #region Private Methods

        private void login() {
            driver.Navigate().GoToUrl("https://www.saltybet.com/authenticate?signin=1");

            IWebElement emailInput = driver.FindElement(By.Id("email"));
            IWebElement pwordInput = driver.FindElement(By.Id("pword"));
            IWebElement loginButton = driver.FindElement(By.CssSelector("input[value='Sign In']"));

            // TODO: replace this with a config file that is gitignored
            emailInput.SendKeys("njdesmarais@gmail.com");
            pwordInput.SendKeys("Nicholas!2");
            loginButton.Click();
        }

        
        private void scrapeStatsPages(int start, int stop) {
            for (int pageNumber = start; pageNumber <= stop; pageNumber++) {
                String table = scrapeTable($"stats?tournamentstats=1&page={pageNumber}");

                List<String> cells = regexMatchesGroup1(table, @"<td(.*)<\/td");

                foreach (String cell in cells) {
                    try {
                        // WaaaWaaa you shouldnt use RegEx to parse HTML... shut the hell up
                        String query = regexMatchGroup1(cell, @"href=""(.*?)""");
                        int tournamentId = idFromHref(query);

                        file.WriteLine(query);
                        //scrapeTournamentPage(query);
                    } catch (Exception e) {
                        pageErrors.Add(cell, e.Message);
                    }
                }
            }
        }

        private void scrapeTournamentPage(String query) {
            string table = scrapeTable(query);

            List<String> cells = regexMatchesGroup1(table, @"<td(.*?)<\/td");
            List<String>.Enumerator cellItr = cells.GetEnumerator();

            while (cellItr.MoveNext()) {

                String matchCell = cellItr.Current;

                if (matchCell.Contains("No data available in table")) return;

                try {
                    // The RegEx RegEcko is coming for youuuuuu
                    String matchQuery = regexMatchGroup1(matchCell, @"href=""(.*?)""");
                    String char1 = regexMatchGroup1(matchCell, @"""redtext"">(.*?)<\/span");
                    String char2 = regexMatchGroup1(matchCell, @"""bluetext"">(.*?)<\/span");
                    int matchId = idFromHref(matchQuery);

                    cellItr.MoveNext();

                    String winnerCell = cellItr.Current;
                    String winner = regexMatchGroup1(winnerCell, @"text"">(.*?)<\/span");

                    cellItr.MoveNext(); // start time
                    cellItr.MoveNext(); // end time
                    cellItr.MoveNext();

                    String bettorsCell = cellItr.Current;
                    String bettorsString = regexMatchGroup1(bettorsCell, @">(\d*)");
                    int bettors = int.Parse(bettorsString);

                    if (winner == "") continue; // match is ongoing

                    file.WriteLine($"Match: {matchId} {char1} {char2} {winner} {bettors}");
                    scrapeMatchPage(matchQuery);
                } catch (Exception e) {
                    tournamentErrors.Add(matchCell, e.Message);
                }
            }
        }

        private void scrapeMatchPage(String query) {
            string table = scrapeTable(query);

            List<String> cells = regexMatchesGroup1(table, @"<td(.*?)<\/td");
            List<String>.Enumerator cellItr = cells.GetEnumerator();

            while (cellItr.MoveNext()) {

                String bettorString = cellItr.Current;

                if (bettorString.Contains("No data available in table")) return;

                try {
                    String pattern = bettorString.Contains("rgb") ? @"\)"">(.*?)<\/span" :
                                 bettorString.Contains("goldtext") ? @"goldtext"">(.*?)<\/span" :
                                                                      @"class="" "">(.*)";
                    String bettor = regexMatchGroup1(bettorString, pattern);

                    cellItr.MoveNext();

                    String wagerString = cellItr.Current;
                    String wagerOn = regexMatchGroup1(wagerString, @"text"">(.*?)</span>");
                    String wagerAmtString = regexMatchGroup1(wagerString, @"class="" "">(\d*) on");
                    int wagerAmt = int.Parse(wagerAmtString);

                    cellItr.MoveNext();

                    String payoutString = cellItr.Current;
                    String wagerPytString = regexMatchGroup1(payoutString, @"class="" "">(-*\d*)");
                    int wagerPyt = int.Parse(wagerPytString);

                    file.WriteLine($"Wager: {bettor} {wagerOn} {wagerAmt} {wagerPyt}");
                } catch (Exception e) {
                    matchErrors.Add(bettorString, e.Message);
                }
            }
        }

        private void writeLogFile() {
            logFile.WriteLine("BGEIN PAGE ERRORS:");
            foreach(KeyValuePair<String,String> kvp in pageErrors) {
                logFile.WriteLine($"Tournament Cell:\n{kvp.Key}\nError:\n{kvp.Value}");
            }
            logFile.WriteLine("BGEIN TOURNAMENT ERRORS:");
            foreach (KeyValuePair<String, String> kvp in tournamentErrors) {
                logFile.WriteLine($"Match Cell:\n{kvp.Key}\nError:\n{kvp.Value}");
            }
            logFile.WriteLine("BGEIN MATCH ERRORS:");
            foreach (KeyValuePair<String, String> kvp in matchErrors) {
                logFile.WriteLine($"Bettor Cell:\n{kvp.Key}\nError:\n{kvp.Value}");
            }
        }

        #endregion

        #region helper functions 
        private String regexMatchGroup1(String input, String pattern) {
            return Regex.Match(input, pattern).Groups[1].ToString();
        }

        private List<String> regexMatchesGroup1(String input, String pattern) {
            return Regex.Matches(input, pattern)
                .Cast<Match>()
                .Select((match) => match.Groups[1].ToString())
                .ToList();
        }

        private String scrapeTable(String query) {
            SendRequest($"https://www.saltybet.com/{query}");

            IWebElement tableBody = driver.FindElement(By.TagName("tbody"));

            return tableBody.GetAttribute("innerHTML");
        }

        private void SendRequest(string url) {
            if (lastRequestTime == null) {
                lastRequestTime = DateTime.Now;
                driver.Navigate().GoToUrl(url);
            } else {
                TimeSpan span = new TimeSpan(0, 0, 12);
                DateTime nextRequestTime = lastRequestTime.Value + span;
                while (DateTime.Now < nextRequestTime) {
                    System.Threading.Thread.Sleep(1);
                }
                lastRequestTime = DateTime.Now;
                driver.Navigate().GoToUrl(url);
            }
        }

        private int idFromHref(String href) {
            String str = regexMatchGroup1(href, @"=(\d*)");
            return int.Parse(str);
        }
        #endregion

        #region IDisposable
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                file.Close();
                file.Dispose();
                logFile.Close();
                logFile.Dispose();
            }
        }
        #endregion
    }
}
