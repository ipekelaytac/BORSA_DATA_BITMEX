using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace BORSA_DATA_BITMEX
{
    public partial class frmMain : Form
    {

        public tools myTools = new tools();
        public DAL myDal = new DAL();

        string exchange_id = "132";
        string exchange = "BitMEX";


        public string constr = "";

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {


            myDal.frmMain = this;

            myTools.frmMain = this;

            myTools.logWriter("Goliath Online");


            constr = System.Configuration.ConfigurationSettings.AppSettings["con"].ToString();
              
            myDal.OpenSQLConnection(constr, myDal.myConnection);

            timer1.Enabled = true;

            timer2.Enabled = false;

            constr = "0";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            timer2.Enabled = true;

        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            timer2.Enabled = false;
            try
            {
                button1_Click(sender, e);
            }
            catch (Exception ex)
            {

                myTools.logWriter("Hata : " + ex.Message);
            }
            timer2.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //GetBitmexAktifInstruments();

            GetCoinDataOHLCV();



        }

        public void GetCoinDataOHLCV()
        {
            myTools.logWriter("GetCoinDataOHLCV Başlıyor...");

            try
            {
                string sql = "select id , ad , sembol , BIRIM  from CIFT_BORSA (nolock) where exchange_id = 132  and aktif = 1 order by volume_24h desc";
                SqlDataReader oku3 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
                while (oku3.Read())
                {
                    int id = Convert.ToInt32(oku3[0].ToString());
                    string ad = oku3[1].ToString();
                    string sembol = oku3[2].ToString();
                    string BIRIM = oku3[3].ToString();

                    try
                    {
                        myTools.logWriter("Veri çekilecek : " + ad);

                        GetCoinDataOHLCV_CoinVerisiCek(id,ad,sembol, BIRIM);

                    }
                    catch (Exception ex)
                    {
                        myTools.logWriter("Hata 1 : " + ex.Message);
                    }


                }
                oku3.Close();

            }
            catch (Exception ex)
            {
                myTools.logWriter("Hata 2 : " + ex.Message);
            }


        }

        public void GetCoinDataOHLCV_CoinVerisiCek(int id , string ad, string sembol, string birim)
        {
            string sql = "select top 1 tarih from DATA_GUNLUK_BORSALAR (nolock) where cift_borsa_id = " + id.ToString() + " order by tarih desc";
            SqlDataReader oku3 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
            DateTime son_data_tarihi = Convert.ToDateTime("01/01/2000");
            while (oku3.Read())
            {
                son_data_tarihi = Convert.ToDateTime(oku3[0].ToString());
            }
            oku3.Close();

            DateTime yeni_data_tarihi = Convert.ToDateTime("01/01/2000");
            if (son_data_tarihi != Convert.ToDateTime("01/01/2000") )
            {
                //yeni_data_tarihi = son_data_tarihi.AddDays(1);
                yeni_data_tarihi = son_data_tarihi;
            }
            else
            {
                yeni_data_tarihi = getIlkDataTarihi(ad);
            }

            System.TimeSpan diff = DateTime.Now.Subtract(yeni_data_tarihi);

            int fark_gun = Convert.ToInt16(Math.Round( (DateTime.Now - yeni_data_tarihi).TotalDays , 0));


            for (int i = 0; i < fark_gun; i++)
            {
                string baslangic_tarihi = yeni_data_tarihi.AddDays(i).ToString("yyyy-MM-ddT00:00:00.0000000K");
                string bitis_tarihi = yeni_data_tarihi.AddDays(i).ToString("yyyy-MM-ddT23:59:59.9999999K");

                myTools.logWriter("Gün Çekiliyor : " + baslangic_tarihi + " - " + ad);

                ProcessGun(ad, baslangic_tarihi, bitis_tarihi , id);
            }

        }

        public void ProcessGun(string ad, string bastar, string bittar , int cift_borsa_id)
        {

            string url = "https://www.bitmex.com/api/v1/trade?symbol=" + ad + "&count=1000&start=baslama_adedi&reverse=false&startTime=" +
                    System.Net.WebUtility.UrlEncode(bastar) + "&endTime=" + System.Net.WebUtility.UrlEncode(bittar);

            int baslama_adedi = 1;

            double open = 0;
            double high = 0;
            double low = 0;
            double close = 0;
            double HacimFrom = 0;
            double HacimTo = 0;

            string deger = "";

            int bu_turda_process_edilen_data_adedi = 0;
            int bu_turda_process_edilen_gecerli_data_adedi = 0;

            int hata_sayisi = 0;

            bool cik = false;
            while (cik == false)
            {
                tools.bekle(1500);
                try
                {
                    myTools.logWriter("Gün Çekiliyor : " + bastar + " - " + ad + " / " + baslama_adedi.ToString());

                    deger = myTools.WebRequestIste(url.Replace("baslama_adedi", baslama_adedi.ToString()));
                    if (deger == "")
                    {
                        myTools.logWriter("Hata Aldık : " + hata_sayisi.ToString());
                        hata_sayisi = hata_sayisi + 1;
                        if (hata_sayisi > 10)
                        {
                            cik = true;
                        }
                        else
                        {
                            tools.bekle(5000);

                        }
                    }
                    else
                    {

                        hata_sayisi = 0;

                        bu_turda_process_edilen_data_adedi = 0;
                        bu_turda_process_edilen_gecerli_data_adedi = 0;
                        if (deger.Contains("timestamp"))
                        {
                            dynamic jObj = JsonConvert.DeserializeObject(deger);

                            foreach (var veri in jObj)
                            {
                                bu_turda_process_edilen_data_adedi = bu_turda_process_edilen_data_adedi + 1;
                                double size = veri.size; // 5000
                                double price = veri.price; //0.00021
                                double hacim = size * price;
                                if ((size != 0) && (price != 0))
                                {
                                    bu_turda_process_edilen_gecerli_data_adedi = bu_turda_process_edilen_gecerli_data_adedi + 1;
                                    if ((bu_turda_process_edilen_gecerli_data_adedi == 1) && (open == 0))
                                    {
                                        open = price;
                                        high = price;
                                        low = price;
                                    }
                                    close = price;

                                    if (close > high)
                                    {
                                        high = close;
                                    }
                                    if (close < low)
                                    {
                                        low = close;
                                    }

                                    HacimFrom = HacimFrom + size;
                                    HacimTo = HacimTo + hacim;

                                }
                            }


                        }

                        if (bu_turda_process_edilen_data_adedi == 0)
                        {
                            cik = true;
                        }
                        else
                        {
                            baslama_adedi = baslama_adedi + 1000;
                        }
                    }






                }
                catch (Exception ex)
                {
                    myTools.logWriter("Hata 3 : " + ex.Message);
                    cik = true;
                }



            }
            if (open == 0)
            {
                high = 0;
                // datası sıfır bulunan günler gibi bir tabloya log at ki
                // daha sonra kontrol edebilelim..
            }
            else
            {
                string sql = "";
                sql = sql + "insert into DATA_GUNLUK_BORSALAR (tarih, cift_borsa_id, d_open, d_high, d_low, d_close, v_from, v_to) values ( ";
                sql = sql + " '" + bastar.Replace("-", "").Substring(0, 8) + "' , " + cift_borsa_id.ToString() + " , " + open.ToString().Replace(",", ".") + " ,   ";
                sql = sql + " " + high.ToString().Replace(",", ".") + " , " + low.ToString().Replace(",", ".") + " , " + close.ToString().Replace(",", ".") + " ,   ";
                sql = sql + " " + HacimFrom.ToString().Replace(",", ".") + " , " + HacimTo.ToString().Replace(",", ".") +
                    " ) ";

                myDal.CommandExecuteNonQuery(sql, myDal.myConnection);
            }

        }


        public DateTime getIlkDataTarihi(string ad)
        {
            DateTime sonuc = Convert.ToDateTime("01/01/2014");

            string url = "https://www.bitmex.com/api/v1/trade?symbol=" + ad + "&count=1&reverse=false";

            string deger = myTools.WebRequestIste(url);

            if (deger.Contains("timestamp"))
            {
                dynamic jObj = JsonConvert.DeserializeObject(deger);

                sonuc = Convert.ToDateTime(jObj[0].timestamp);

            }

            return sonuc;
        }


        public void GetBitmexAktifInstruments()
        {

            myTools.logWriter(" GetBitmexAktifInstruments Başlıyor...");


            string url = "https://www.bitmex.com/api/v1/instrument/active";

            string sonuc = myTools.WebRequestIste(url);

            if (sonuc.Contains("rootSymbol"))
            {

            

                dynamic jObj = JsonConvert.DeserializeObject(sonuc);

                foreach (var veri in jObj)
                {
                    string csembol = veri.symbol; // XRPM20
                    string rootSymbol = veri.rootSymbol; //XRP
                    string cbirim = csembol.Replace(rootSymbol,"");

                    csembol = rootSymbol;

                    int sembol_id = -1;
                    int birim_id = -1;

                    if (csembol=="XBT")
                    {
                        csembol = "BTC";
                    }

                    string sql = "select id from SEMBOLLER (nolock) where sembol='" + csembol + "'";
                    SqlDataReader oku2 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
                    sembol_id = -1;
                    while (oku2.Read())
                    {
                        sembol_id = Convert.ToInt32(oku2[0].ToString());
                    }
                    oku2.Close();
                    
                    if (sembol_id > 0)
                    {

                        sql = "select id from PARA (nolock) where kisaltma='" + cbirim + "'";
                        SqlDataReader oku3 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
                        birim_id = -1;
                        while (oku3.Read())
                        {
                            birim_id = Convert.ToInt32(oku3[0].ToString());
                        }
                        oku3.Close();


                        if (birim_id == -1)
                        {
                            sql = "";
                            sql = sql + "insert into para (ad , kisaltma , country , USD  ) values ( ";
                            sql = sql + " '" + cbirim + "', '" + cbirim + "', '', 0 ) ";

                            myDal.CommandExecuteNonQuery(sql, myDal.myConnection);

                            sql = "select id from PARA (nolock) where kisaltma='" + cbirim + "'";
                            oku3 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
                            birim_id = -1;
                            while (oku3.Read())
                            {
                                birim_id = Convert.ToInt32(oku3[0].ToString());
                            }
                            oku3.Close();
                        }

                    }


                    int xid = -1;
                    sql = "select id from CIFT (nolock) where sembol='" + csembol + "' and BIRIM = '" + cbirim + "' ";
                    SqlDataReader oku4 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);

                    while (oku4.Read())
                    {
                        xid = Convert.ToInt32(oku4[0].ToString());
                    }
                    oku4.Close();

                    if (xid < 0)
                    {
                        sql = "";
                        sql = sql + "insert into CIFT (ad , SEMBOL , BIRIM , sembol_id , birim_id , tip  ) values ( ";
                        sql = sql + " '" + csembol + cbirim + "', '" + csembol + "', '" + cbirim + "', ";
                        sql = sql + " '" + sembol_id + "', '" + birim_id + "', 2 ) ";

                        myDal.CommandExecuteNonQuery(sql, myDal.myConnection);
                    }
                    
                    xid = -1;
                    sql = "select id from CIFT_BORSA (nolock) where sembol='" + csembol + "' and BIRIM = '" + cbirim + "'  and exchange_id = '" + exchange_id + "' ";
                    SqlDataReader oku6 = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);

                    while (oku6.Read())
                    {
                        xid = Convert.ToInt32(oku6[0].ToString());
                    }
                    oku6.Close();

                    if (xid < 0)
                    {
                        sql = "";
                        sql = sql + "insert into CIFT_BORSA (ad , SEMBOL , BIRIM , sembol_id , birim_id , tip , exchange_id ) values ( ";
                        sql = sql + " '" + csembol + cbirim + "', '" + csembol + "', '" + cbirim + "', ";
                        sql = sql + " '" + sembol_id + "', '" + birim_id + "', 2 , '" + exchange_id + "' ) ";

                        myDal.CommandExecuteNonQuery(sql, myDal.myConnection);

                        myTools.logWriter("CIFT EKLEME : " + csembol + " -> " + cbirim + " --> " + exchange);
                        Application.DoEvents();

                    }

                    Console.Write(veri);

                }

            }
            else
            {
                myTools.logWriter(url);
                myTools.logWriter("Çağrılırken hata alındı !");
                myTools.logWriter(sonuc);
            }








        }

            


    }
}
