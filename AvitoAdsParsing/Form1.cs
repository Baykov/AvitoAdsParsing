using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
//using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Tesseract;

namespace AvitoAdsParsing
{
    public partial class Form1 : Form
    {
        List<int> idList = new List<int>();
        List<int> newIdList = new List<int>();
        DateTime startime = DateTime.Now;

        List<asuwObject> objectList = new List<asuwObject>();
        private string dirName = "";

        Timer t = new Timer();

        public Form1()
        {
            InitializeComponent();
            DateTime today = DateTime.Now;
            int year = today.Year;
            int month = today.Month;
            int day = today.Day;
            dirName = day.ToString() + "_" + month.ToString() + "_" + year.ToString();
            if (!Directory.Exists("pages/" + dirName))
            {
                Directory.CreateDirectory("pages/" + dirName);
            }
            if (!Directory.Exists("idlists/" + dirName))
            {
                Directory.CreateDirectory("idlists/" + dirName);
            }
            if (!Directory.Exists("export/" + dirName))
            {
                Directory.CreateDirectory("export/" + dirName);
            }
            if (!Directory.Exists("irrpages/" + dirName))
            {
                Directory.CreateDirectory("irrpages/" + dirName);
            }
            if (!Directory.Exists("irridlists/" + dirName))
            {
                Directory.CreateDirectory("irridlists/" + dirName);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(startParser);
           // startParser();
        }

        private void exportOneObject(List<asuwObject> objectList)
        {
            try
            {
                string time = DateTime.Now.Ticks.ToString();
                XmlDocument xmlDocumentObjects = new XmlDocument();
                XmlSerializer serializerObjects = new XmlSerializer(objectList.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializerObjects.Serialize(stream, objectList);
                    stream.Position = 0;
                    xmlDocumentObjects.Load(stream);
                    xmlDocumentObjects.Save("export_" + time + ".xml");
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
            objectList.Clear();
        }


        private void startParser()
        {
            
            // получаем список уникальных идентификаторов объяв исследуя все файлы в папке экспорт
            getIds();

            var driver = new FirefoxDriver();
            var linkToParseStr = linkToParse.Text.Trim();
            if (linkToParseStr.Length > 0)
            {
                asuwObject obj = new asuwObject() { link = linkToParseStr };
                parsePage(driver, obj, true);
                return;
            }
            driver.Navigate().GoToUrl("https://www.avito.ru/profile/login");

            var login = driver.FindElement(By.Name("login"));
            login.Clear();
            login.SendKeys("baykovsky@meta.ua");

            var password = driver.FindElement(By.Name("password"));
            password.Clear();
            password.SendKeys("123123qwer");

            var submit = driver.FindElement(By.ClassName("btn-medium"));
            submit.Click();

            driver.Navigate().GoToUrl("https://www.avito.ru/yaroslavskaya_oblast/kvartiry/prodam/novostroyka?bt=0");

            // получаем количество страниц
            // int pagesCount = 2;
           // int pagesCount = getPagesCount(driver);
            for (int i = 1; i < 5; i++)
            {
                List<asuwObject> objectPrepareList = new List<asuwObject>();

                // парсинг предварительный с проверкой на уникальность идентификаторов
                objectPrepareList = parsePrepareList(driver, i);

                // сразу сохранение в сегодняшнюю папку новых объяв постранично
                XmlDocument xmlDocumentLinks = new XmlDocument();
                XmlSerializer serializerLinks = new XmlSerializer(objectPrepareList.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializerLinks.Serialize(stream, objectPrepareList);
                    stream.Position = 0;
                    xmlDocumentLinks.Load(stream);
                    xmlDocumentLinks.Save("pages/" + dirName + "/exportprepares_page" + i + ".xml");
                    stream.Close();
                }

                // получаем сегодняшние УНИКАЛЬНЫЕ объекты из предварительного парсинга
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load("pages/"+ dirName+"/exportprepares_page" + i + ".xml");
                foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
                {
                    var ser = new XmlSerializer(typeof(asuwObject));
                    var asuwObj = (asuwObject)ser.Deserialize(new StringReader(node.OuterXml));

                    if (objectList.Count > 9 || ((xDoc.DocumentElement.ChildNodes.Count < 9) && (objectList.Count == xDoc.DocumentElement.ChildNodes.Count)))
                    {
                        exportObjects();
                    }
                    // собственно подробный парсинг объявы 
                    parsePage(driver, asuwObj);
                }
            }


            // обновление сегодняшнего списка уникальных объяв
            setIds();
            SystemSounds.Hand.Play();
            // экспорт после подробного парсинга
            driver.Dispose();
        }

        private void exportObjects()
        {
            try
            {
                string time = DateTime.Now.Ticks.ToString();
                XmlDocument xmlDocumentObjects = new XmlDocument();
                XmlSerializer serializerObjects = new XmlSerializer(objectList.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializerObjects.Serialize(stream, objectList);
                    stream.Position = 0;
                    xmlDocumentObjects.Load(stream);
                    xmlDocumentObjects.Save("export/" + dirName + "/export_" + time + ".xml");
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
            objectList.Clear();
        }

        private void getIds(string irr = "")
        {
            DirectoryInfo dirInfo = new DirectoryInfo(irr+"pages/");

            foreach(DirectoryInfo dirInfo1 in  dirInfo.GetDirectories())
            {
                foreach (FileInfo f in dirInfo1.GetFiles())
                {
                    XmlDocument xDoc1 = new XmlDocument();
                    if (!File.Exists(f.FullName))
                    {
                        continue;
                    }
                    xDoc1.Load(f.FullName);
                    foreach (XmlNode node1 in xDoc1.DocumentElement.ChildNodes)
                    {
                        var ser1 = new XmlSerializer(typeof(asuwObject));
                        var asuwObj = (asuwObject)ser1.Deserialize(new StringReader(node1.OuterXml));
                        idList.Add(asuwObj.id);
                    }

                   // Console.WriteLine(f.FullName);
                }
                
            }
            //XmlDocument xDoc = new XmlDocument();
            //if (!File.Exists("idlists/" + dirName + "/exportidlist.xml"))
            //{
            //    return;
            //}
           
            //xDoc.Load("idlists/" + dirName+"/exportidlist.xml");
            //foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
            //{
            //    var ser = new XmlSerializer(typeof(int));
            //    var id = (int)ser.Deserialize(new StringReader(node.OuterXml));
            //    idList.Add(id);
            //}

        }

        private void setIds(string irr = "")
        {
            //for (int i = 1; i < pagesCount; i++)
            //{
            //    XmlDocument xDoc = new XmlDocument();
            //    xDoc.Load("pages/" + dirName + "/exportprepares_page" + i + ".xml");
            //    foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
            //    {
            //        var ser = new XmlSerializer(typeof(asuwObject));
            //        var asuwObj = (asuwObject)ser.Deserialize(new StringReader(node.OuterXml));
            //        idList.Add(asuwObj.id);
            //    }
            //}
            XmlDocument xmlDocumentObjects1 = new XmlDocument();
            XmlSerializer serializerObjects1 = new XmlSerializer(newIdList.GetType());
            using (MemoryStream stream = new MemoryStream())
            {
                serializerObjects1.Serialize(stream, newIdList);
                stream.Position = 0;
                xmlDocumentObjects1.Load(stream);
                xmlDocumentObjects1.Save(irr+"idlists/" + dirName + "/exportidlist.xml");
                stream.Close();
            }

        }

        private List<asuwObject> parsePrepareList(FirefoxDriver driver, int i)
        {
            List<asuwObject> localObjList = new List<asuwObject>();

            try
            {
                driver.Navigate()
                    .GoToUrl("https://www.avito.ru/yaroslavskaya_oblast/kvartiry/prodam/novostroyka?bt=0&p=" + i);

                var items = driver.FindElements(By.ClassName("item_table"));
                foreach (IWebElement item in items)
                {
                    var objectPrepare = new asuwObject();
                    try
                    {
                        int id = Convert.ToInt32(item.GetAttribute("id").Replace("i", ""));
                        if (idList.Contains(id)) { continue;}
                        objectPrepare.id = id;
                        newIdList.Add(objectPrepare.id);
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.id = 0;
                    }
                    var description = item.FindElement(By.ClassName("description"));
                    try
                    {
                        objectPrepare.rajon =
                            description.FindElement(By.ClassName("address")).Text.Replace("р-н", "").Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.rajon = "";
                    }
                    try
                    {
                        objectPrepare.title =
                            description.FindElement(By.ClassName("title")).FindElement(By.TagName("a")).Text.Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.title = "";
                    }
                    try
                    {
                        objectPrepare.rooms = objectPrepare.title.Substring(0, 1);
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.rooms = "";
                    }
                    var titleArr = objectPrepare.title.Split(',');
                    try
                    {
                        objectPrepare.s_m2 = titleArr[1].Replace("м²", "");
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.s_m2 = "";
                    }
                    var et = titleArr[2].Replace("эт.", "").Split('/');

                    try
                    {
                        objectPrepare.etaj = et[0];
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.etaj = "";
                    }
                    try
                    {
                        objectPrepare.etajnost = et[1];
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.etajnost = "";
                    }

                    try
                    {
                        objectPrepare.link =
                            description.FindElement(By.ClassName("title"))
                                .FindElement(By.TagName("a"))
                                .GetAttribute("href")
                                .Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.link = "";
                    }
                    try
                    {
                        objectPrepare.price =
                            description.FindElement(By.ClassName("about")).Text.Replace("руб.", "").Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.price = "";
                    }
                    try
                    {
                        objectPrepare.an =
                            description.FindElement(By.ClassName("data")).FindElements(By.TagName("p"))[0].Text.Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.an = "";
                    }
                    try
                    {
                        objectPrepare.city =
                            description.FindElement(By.ClassName("data")).FindElements(By.TagName("p"))[1].Text.Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.city = "";
                    }

                    localObjList.Add(objectPrepare);
                }

                //var links = driver.FindElementsByXPath("//h3[@class='title']/a");
                //foreach (IWebElement link in links)
                //{
                //    linkList.Add(link.GetAttribute("href"));
                //}

            return localObjList;
            }
            catch (Exception ex)
            {
                parsePrepareList(driver, i);
            }
            return localObjList;
        }

        private void parsePage(FirefoxDriver driver, asuwObject obj, bool onlyLink = false)
        {
            driver.Navigate().GoToUrl(obj.link);
            try
            {
                driver.FindElement(By.ClassName("description__phone-insert"));
            }
            catch (Exception ex)
            {
                return;
            }
            IJavaScriptExecutor js = driver as IJavaScriptExecutor;

            try
            {
                obj.descr = driver.FindElement(By.ClassName("description-text")).Text;
            }
            catch (Exception ex)
            {
                obj.descr = "";
            }
            try
            {
                obj.seller = driver.FindElement(By.Id("seller")).Text;
            }
            catch (Exception ex)
            {
                obj.seller = "";
            }
            try
            {
                obj.adress = driver.FindElement(By.Id("toggle_map")).Text;
            }
            catch (Exception ex)
            {
                obj.adress = "";
            }
            try
            {
                obj.phone = parsePhone(driver, js, obj);
            }
            catch (Exception ex)
            {
                parsePage(driver, obj);
            }
            
            
            obj.fotos = new List<string>();


            try
            {
                var fotos = driver.FindElements(By.ClassName("gallery-link"));
                int countFotos = 0;
                foreach (IWebElement foto in fotos)
                {
                    string fotohref = foto.GetAttribute("href");
                    if (fotohref.Contains("640x480") && countFotos<12)
                    {
                        countFotos++;
                        obj.fotos.Add(fotohref);
                    }
                }
            }
            catch (Exception ex)
            {
                obj.phone = "";
            }

            objectList.Add(obj);
            if (onlyLink)
            {
                exportOneObject(objectList);
            }


        }

        private string parsePhone(FirefoxDriver driver, IJavaScriptExecutor js, asuwObject obj)
        {
            string returnPhone = "";
            var phoneElem = driver.FindElement(By.ClassName("description__phone-insert"));
            phoneElem.Click();
            var phoneImg =
                (string) js.ExecuteScript("return document.getElementsByClassName('description__phone-img')[0].src;");
            phoneImg = phoneImg.Replace("data:image/png;base64,", "");
            string localFilename = @"phones\" + obj.id + ".jpg";

            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(phoneImg)))
            {
                using (Bitmap bm2 = new Bitmap(ms))
                {
                    bm2.Save(localFilename);
                }
            }

            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                var b = Image.FromFile(localFilename);
                Image result = new Bitmap(1024, 165);
                using (Graphics g = Graphics.FromImage((Image) result))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.Low;
                    g.DrawImage(b, 0, 0, 1024, 165);
                    g.Dispose();
                }
                result.Save(@"111.png");

                using (var imgq = Pix.LoadFromFile(@"111.png"))
                {
                    var i = 1;
                    using (var page = engine.Process(imgq))
                    {
                        returnPhone = page.GetText().Replace("-", "").Replace(" ", "");
                    }
                }
            }
            return returnPhone;
        }

        private int getPagesCount(FirefoxDriver driver)
        {
            try
            {
                IWebElement lastPageLink = driver.FindElementByLinkText("Последняя");
                string hrefLastPAge = lastPageLink.GetAttribute("href");

                Regex regex = new Regex("p=(..)");
                return Convert.ToInt32(regex.Match(hrefLastPAge).Groups[1].Value);
            }
            catch (Exception ex)
            {
                return 10;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(startIrrParser);
        }

        private void startIrrParser()
        {
            getIds("irr");
            FirefoxProfile p = new FirefoxProfile();
            p.SetPreference("javascript.enabled", false);
            
            var driver = new FirefoxDriver(p);

            var linkToParseStr = linkToParse.Text.Trim();
            if(linkToParseStr.Length>0)
            {
                asuwObject obj = new asuwObject(){link=linkToParseStr};
                parsePageIrr(driver, obj, true);
                return;
            }

            //http://yaroslavl.irr.ru/real-estate/apartments-sale/new/page_len60/page2/

            

            for (int i = 1; i < 5; i++)
            {
                List<asuwObject> objectPrepareList = new List<asuwObject>();

                // парсинг предварительный с проверкой на уникальность идентификаторов
                objectPrepareList = parsePrepareListIrr(driver, i);

                // сразу сохранение в сегодняшнюю папку новых объяв постранично
                XmlDocument xmlDocumentLinks = new XmlDocument();
                XmlSerializer serializerLinks = new XmlSerializer(objectPrepareList.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializerLinks.Serialize(stream, objectPrepareList);
                    stream.Position = 0;
                    xmlDocumentLinks.Load(stream);
                    xmlDocumentLinks.Save("irrpages/" + dirName + "/exportprepares_page" + i + ".xml");
                    stream.Close();
                }

                // получаем сегодняшние УНИКАЛЬНЫЕ объекты из предварительного парсинга
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load("irrpages/" + dirName + "/exportprepares_page" + i + ".xml");
                foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
                {
                    var ser = new XmlSerializer(typeof(asuwObject));
                    var asuwObj = (asuwObject)ser.Deserialize(new StringReader(node.OuterXml));

                    if (objectList.Count > 9 || ((xDoc.DocumentElement.ChildNodes.Count < 9) && (objectList.Count == xDoc.DocumentElement.ChildNodes.Count)))
                    {
                        exportObjects();
                    }
                    // собственно подробный парсинг объявы 
                    parsePageIrr(driver, asuwObj);
                }
            }


            // обновление сегодняшнего списка уникальных объяв
            setIds("irr");
            SystemSounds.Hand.Play();
            driver.Dispose();
        }


        private List<asuwObject> parsePrepareListIrr(FirefoxDriver driver, int i)
        {
            List<asuwObject> localObjList = new List<asuwObject>();

            try
            {
                if (i == 0)
                {
                    driver.Navigate().GoToUrl("http://yaroslavl.irr.ru/real-estate/apartments-sale/new/page_len60/");
                }
                else
                {
                    driver.Navigate()
                        .GoToUrl("http://yaroslavl.irr.ru/real-estate/apartments-sale/new/page_len60/page" + i);
                }

                var items = driver.FindElements(By.ClassName("productBlock"));
                foreach (IWebElement item in items)
                {
                    var objectPrepare = new asuwObject();
                    try
                    {
                        int id = Convert.ToInt32(item.GetAttribute("data-item-id"));
                        objectPrepare.link = item.GetAttribute("href");
                        if (idList.Contains(id)) { continue; }
                        objectPrepare.id = id;
                        newIdList.Add(objectPrepare.id);
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.id = 0;
                    }

                    var addInfo2 = item.FindElement(By.ClassName("productInfoCol-addInfo2")).FindElement(By.ClassName("productParameter")).Text.Replace("Эт.", "").Trim();
                    //     	                Эт. 2 / 9     	            //
                    var etArr = addInfo2.Split('/');

                    try
                    {
                        objectPrepare.s_m2 = item.FindElement(By.ClassName("productInfoCol-addInfo1")).FindElement(By.ClassName("productParameter")).Text.Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.s_m2 = "";
                    }

                    try
                    {
                        objectPrepare.etaj = etArr[0];
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.etaj = "";
                    }

                    try
                    {
                        objectPrepare.etajnost = etArr[1];
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.etajnost = "";
                    }

                    try
                    {
                        objectPrepare.price = item.FindElement(By.ClassName("productPrice")).Text.Replace("руб.", "").Trim();
                    }
                    catch (Exception ex)
                    {
                        objectPrepare.price = "";
                    }

                    localObjList.Add(objectPrepare);
                }

                //var links = driver.FindElementsByXPath("//h3[@class='title']/a");
                //foreach (IWebElement link in links)
                //{
                //    linkList.Add(link.GetAttribute("href"));
                //}

                return localObjList;
            }
            catch (Exception ex)
            {
                parsePrepareList(driver, i);
            }
            return localObjList;
        }
        private void parsePageIrr(FirefoxDriver driver, asuwObject obj, bool onlyLink = false)
        {
            driver.Navigate().GoToUrl(obj.link);

            try
            {
                var product_characts  = driver.FindElementsByClassName("productPage__characteristicsItemValue");
                if (product_characts.Count >0)
                {
                    obj.rooms = product_characts[0].Text.Trim();//2
                }
                else
                {
                    ReadOnlyCollection<IWebElement> propertyNames = driver.FindElementsByClassName("propertyName");
                    ReadOnlyCollection<IWebElement> propertyValues = driver.FindElementsByClassName("propertyValue");
                    for (int i = 0; i < propertyNames.Count; i++)
                    {
                        if (propertyNames[i].Text == "Район города:")
                        {
                            obj.rajon = propertyValues[i].Text.Trim();
                        }

                        if (propertyNames[i].Text == "Комнат в квартире:")
                        {
                            obj.rooms = propertyValues[i].Text.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                obj.rooms = "";
            }

            try
            {
                obj.city = driver.FindElement(By.ClassName("topline--city")).Text;
            }
            catch (Exception ex)
            {
                try
                {
                    var product_characts = driver.FindElementsByClassName("productPage__infoTextBold");
                    foreach (IWebElement elem in product_characts)
                    {
                        if (elem.Text.Split(',').Length > 0)
                        {
                            obj.adress = elem.Text;
                            obj.city = elem.Text.Split(',')[0];
                        }
                    }
                }
                catch (Exception ex1)
                {
                    obj.city = "";
                }
                
            }

            try
            {
                obj.descr = driver.FindElement(By.ClassName("advertDescriptionText")).Text;
            }
            catch (Exception ex)
            {
                try
                {
                    obj.descr = driver.FindElement(By.ClassName("js-productPageDescription")).Text;
                }
                catch (Exception ex1)
                {
                    obj.descr = "";
                }
            }

            try
            {
                obj.seller = driver.FindElement(By.XPath("//i[@class='icon_house']/following-sibling::div[1]")).Text;
            }
            catch (Exception ex)
            {
                try
                {
                    obj.seller = driver.FindElement(By.ClassName("productPage__infoTextBold_inline")).Text;

                }
                catch (Exception ex1)
                {
                    obj.seller = "";
                } 
            }

            try
            {
                obj.adress = driver.FindElement(By.XPath("//i[@class='icon_spot']/following-sibling::div[1]")).Text;
            }
            catch (Exception ex)
            {
                try
                {
                    obj.adress = driver.FindElement(By.ClassName("js-showOnMap")).Text;
                }
                catch (Exception weq)
                {
                    try
                    {
                        var product_characts = driver.FindElementsByClassName("productPage__infoTextBold");
                        foreach (IWebElement elem in product_characts)
                        {
                            if (elem.Text.Split(',').Length > 0)
                            {
                                obj.adress = elem.Text;
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        obj.adress = "";
                    }
                }
            }

            try
            {
                var phoneElem = driver.FindElement(By.ClassName("js-showProductPhone"));
                var encodedPhone = phoneElem.GetAttribute("data-phone").Trim();
                obj.phone = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPhone));

               // obj.phone = parsePhoneIrr(driver);
            }
            catch (Exception ex)
            {
                try
                {
                    var phoneElem = driver.FindElement(By.ClassName("productPage__phoneText"));
                    var encodedPhone = phoneElem.GetAttribute("data-phone").Trim();
                    obj.phone = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPhone));
                }
                catch (Exception ex1)
                {
                    obj.phone = "";
                }
               // parsePageIrr(driver, obj, onlyLink);
            }


            obj.fotos = new List<string>();


            try
            {
                var advertPhotosFotorama = driver.FindElement(By.ClassName("advertPhotosFotorama"));
                var fotos = advertPhotosFotorama.FindElements(By.TagName("a"));

                int countFotos = 0;
                foreach (IWebElement foto in fotos)
                {
                    string fotosrc = foto.GetAttribute("href");
                    if (fotosrc.Contains("orig") && countFotos < 12)
                    {
                        countFotos++;
                        obj.fotos.Add(fotosrc);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var fotos = driver.FindElements(By.ClassName("productPage__galleryImage"));


                    int countFotos = 0;
                    foreach (IWebElement foto in fotos)
                    {
                        string fotosrc = foto.GetAttribute("href");
                        if (fotosrc.Contains("orig") && countFotos < 12)
                        {
                            countFotos++;
                            obj.fotos.Add(fotosrc);
                        }
                    }
                }
                catch (Exception ex1)
                {
                    obj.fotos = null;
                }

            }
            //1-к квартира, 44 м², 6/10 эт.
            obj.title = obj.rooms + "-к квартира, " + obj.s_m2 + ", " + obj.etaj + "/" + obj.etajnost + " эт.";
            objectList.Add(obj);
            if (onlyLink)
            {
                exportOneObject(objectList);
            }

        }

        private string parsePhoneIrr(FirefoxDriver driver)
        {
            var phoneElem = driver.FindElement(By.ClassName("js-showProductPhone"));
            var encodedPhone = phoneElem.GetAttribute("data-phone").Trim();
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedPhone));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            t.Interval = 30*60*1000;
            t.Start();
            t.Tick += t_Tick;

        }

        void t_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;

            if (now.AddMinutes(30).CompareTo(startime) > 0)
            {
                startime = DateTime.Now;
                Console.WriteLine(@"start " + now);
                Task.Factory.StartNew(startParser);
                Task.Factory.StartNew(startIrrParser);
            }



            int hour = now.Hour;
            int min = now.Minute;

        }


    }

    public class asuwObject : object
    {
        public int id { get; set; }
        public string title { get; set; }
        public string rooms { get; set; }
        public string s_m2 { get; set; }
        public string etaj { get; set; }
        public string an { get; set; }
        public string link { get; set; }
        public string etajnost { get; set; }
        public string price { get; set; }
        public string rajon { get; set; }
        public string descr { get; set; }
        public string seller { get; set; }
        public string city { get; set; }
        public string adress { get; set; }
        public string phone { get; set; }
        public string originalLink { get; set; }
        public List<string> fotos { get; set; }
    }







}
