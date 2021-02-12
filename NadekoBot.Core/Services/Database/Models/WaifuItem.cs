using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class WaifuItem : DbEntity
    {
        public int? WaifuInfoId { get; set; }
        public int? GifterWaifuInfoId { get; set; }
        public string ItemEmoji { get; set; }
        public int Price { get; set; }
        public int Count { get; set; }
        public ItemName Item { get; set; }

        public enum ItemName
        {
            Cookie,
            Candy,
            Donut,
            Rose,
            Bouquet,
            Tea,
            Coffee,
            Pizza,
            Chocolate,
            Ramen,
            Icecream,
            Sake,
            Sushi,
            Surprise,
            LoveLetter,
            Manga,
            Cake,
            Christmas,
            Spider,
            Snake,
            Mask,
            Guitar,
            Kimono,
            Iphone,
            Laptop,
            Ring,
            Honor,
            Diamond,
            Crown,
            Castle,
            Newcomer,
            Unicorn,
            Dragon,
            Star,
            Moon,
            Love,
        }

        public WaifuItem()
        {

        }

        public WaifuItem(string itemEmoji, int price, ItemName item)
        {
            ItemEmoji = itemEmoji;
            Price = price;
            Item = item;
        }

        public static WaifuItem GetItemObject(ItemName itemName, int mult)
        {
            WaifuItem wi;
            switch (itemName)
            {
                case ItemName.Cookie:
                    wi = new WaifuItem("🍪", 10, itemName);
                    break;
                case ItemName.Candy:
                    wi = new WaifuItem("🍬", 20, itemName);
                    break;
                case ItemName.Donut:
                    wi = new WaifuItem("🍩", 30, itemName);
                    break;
                case ItemName.Rose:
                    wi = new WaifuItem("🌹", 50, itemName);
                    break;
                case ItemName.Bouquet:
                    wi = new WaifuItem("💐", 70, itemName);
                    break;
                case ItemName.Tea:
                    wi = new WaifuItem("🍵", 100, itemName);
                    break;
                case ItemName.Coffee:
                    wi = new WaifuItem("☕", 100, itemName);
                    break;
                case ItemName.Pizza:
                    wi = new WaifuItem("🍕", 150, itemName);
                    break;
                case ItemName.Chocolate:
                    wi = new WaifuItem("🍫", 200, itemName);
                    break;
                case ItemName.Ramen:
                    wi = new WaifuItem("🍜", 200, itemName);
                    break;
                case ItemName.Icecream:
                    wi = new WaifuItem("🍨", 200, itemName);
                    break;
                case ItemName.Sake:
                    wi = new WaifuItem("🍶", 300, itemName);
                    break;
                case ItemName.Sushi:
                    wi = new WaifuItem("🍣", 400, itemName);
                    break;
                case ItemName.Surprise:
                    wi = new WaifuItem("🎁", 500, itemName);
                    break;
                case ItemName.LoveLetter:
                    wi = new WaifuItem("💌", 650, itemName);
                    break;
                case ItemName.Manga:
                    wi = new WaifuItem("📓", 800, itemName);
                    break;
                case ItemName.Cake:
                    wi = new WaifuItem("🍰", 1000, itemName);
                    break;
                case ItemName.Christmas:
                    wi = new WaifuItem("🎄", 1300, itemName);
                    break;
                case ItemName.Spider:
                    wi = new WaifuItem("🕷️", 1500, itemName);
                    break;
                case ItemName.Snake:
                    wi = new WaifuItem("🐍", 1700, itemName);
                    break;
                case ItemName.Mask:
                    wi = new WaifuItem("👹", 3000, itemName);
                    break;
                case ItemName.Guitar:
                    wi = new WaifuItem("🎸", 5000, itemName);
                    break;
                case ItemName.Kimono:
                    wi = new WaifuItem("👘", 7000, itemName);
                    break;
                case ItemName.Iphone:
                    wi = new WaifuItem("📱", 8000, itemName);
                    break;
                case ItemName.Laptop:
                    wi = new WaifuItem("💻", 10000, itemName);
                    break;
                case ItemName.Ring:
                    wi = new WaifuItem("💍", 15000, itemName);
                    break;
                case ItemName.Honor:
                    wi = new WaifuItem("🏅", 15000, itemName);
                    break;
                case ItemName.Diamond:
                    wi = new WaifuItem("💎", 20000, itemName);
                    break;
                case ItemName.Crown:
                    wi = new WaifuItem("👑", 25000, itemName);
                    break;
                case ItemName.Castle:
                    wi = new WaifuItem("🏰", 30000, itemName);
                    break;
                case ItemName.Newcomer:
                    wi = new WaifuItem("👾", 50000, itemName);
                    break;
                case ItemName.Unicorn:
                    wi = new WaifuItem("🦄", 50000, itemName);
                    break;
                case ItemName.Dragon:
                    wi = new WaifuItem("🐲", 50000, itemName);
                    break;
                case ItemName.Star:
                    wi = new WaifuItem("🌟", 99999, itemName);
                    break;
                case ItemName.Moon:
                    wi = new WaifuItem("🌕", 100000, itemName);
                    break;
                case ItemName.Love:
                    wi = new WaifuItem("💝", 200000, itemName);
                    break;
                default:
                    throw new ArgumentException("Item is not implemented", nameof(itemName));
            }
            wi.Price = wi.Price * mult;
            return wi;
        }
    }
}


/*
🍪 Cookie 10
🌹  Rose 50
💌 Love Letter 100
🍫  Chocolate 200
🍚 Rice 400
🎟  Movie Ticket 800
📔 Book 1.5k
💄  Lipstick 3k
💻 Laptop 5k
🎻 Violin 7.5k
💍 Ring 10k
*/
