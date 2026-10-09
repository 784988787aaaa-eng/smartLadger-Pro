namespace SmartLedger.Infrastructure.Reporting;
public static class ArabicNumberToWords
{
    private static readonly string[] Ones={"","واحد","اثنان","ثلاثة","أربعة","خمسة","ستة","سبعة","ثمانية","تسعة","عشرة","أحد عشر","اثنا عشر","ثلاثة عشر","أربعة عشر","خمسة عشر","ستة عشر","سبعة عشر","ثمانية عشر","تسعة عشر"};
    private static readonly string[] Tens={"","","عشرون","ثلاثون","أربعون","خمسون","ستون","سبعون","ثمانون","تسعون"};
    private static readonly string[] S={"","ألف","مليون","مليار","تريليون"};private static readonly string[] D={"","ألفان","مليونان","ملياران","تريليونان"};private static readonly string[] P={"","آلاف","ملايين","مليارات","تريليونات"};
    public static string ToWords(decimal amount,string currency="ريال"){if(amount<0)return "سالب "+ToWords(decimal.Abs(amount),currency);var whole=decimal.Truncate(amount);var f=(int)decimal.Round((amount-whole)*100m,0,MidpointRounding.AwayFromZero);var text=whole==0?"صفر":Integer((long)whole);text+=" "+currency;if(f>0)text+=" و"+Integer(f)+" هللة";return "فقط "+text+" لا غير";}
    private static string Integer(long n){if(n==0)return "صفر";var a=new List<string>();var g=0;while(n>0){var c=(int)(n%1000);if(c>0)a.Insert(0,Chunk(c,g));n/=1000;g++;}return string.Join(" و",a);}
    private static string Chunk(int n,int g){var t=Under1000(n);if(g==0)return t;if(n==1)return S[g];if(n==2)return D[g];if(n<=10)return $"{t} {P[g]}";return $"{t} {S[g]}";}
    private static string Under1000(int n){var h=n/100;var r=n%100;var a=new List<string>();if(h>0)a.Add(h switch{1=>"مائة",2=>"مائتان",3=>"ثلاثمائة",4=>"أربعمائة",5=>"خمسمائة",6=>"ستمائة",7=>"سبعمائة",8=>"ثمانمائة",_=>"تسعمائة"});if(r>0)a.Add(r<20?Ones[r]:r%10==0?Tens[r/10]:$"{Ones[r%10]} و{Tens[r/10]}");return string.Join(" و",a);}
}
