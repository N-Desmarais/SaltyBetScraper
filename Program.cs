using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaltyBetScraper {
    class Program {
        static void Main(string[] args) {
            SaltyBetScraper scraper = new SaltyBetScraper(1, 2300);
            scraper.scrapeCompendium();
            scraper.Dispose();
        }
    }
}
