using System;
using System.Text;
using System.Collections;

namespace Jxc.Utility
{
	/// <summary>
	/// PyConverter 的摘要说明。
	/// </summary>
	public class PyConverter
	{
		public PyConverter()
		{ 
		}

		private static String CodeData = "cjwgnspgcenegypbtwxzdxykygtpjnmjqmbsgzscyjsyyfpggbzgydywjkgaljswkbjqhyjwpdzlsgmr"
			+ "ybywwccgznkydgttngjeyekzydcjnmcylqlypyqbqrpzslwbdgkjfyxjwcltbncxjjjjcxdtqsqzycdxxhgckbphffss"
			+ "pybgmxjbbyglbhlssmzmpjhsojnghdzcdklgjhsgqzhxqgkezzwymcscjnyetxadzpmdssmzjjqjyzcjjfwqjbdzbjgd"
			+ "nzcbwhgxhqkmwfbpbqdtjjzkqhylcgxfptyjyyzpsjlfchmqshgmmxsxjpkdcmbbqbefsjwhwwgckpylqbgldlcctnma"
			+ "eddksjngkcsgxlhzaybdbtsdkdylhgymylcxpycjndqjwxqxfyyfjlejbzrwccqhqcsbzkymgplbmcrqcflnymyqmsqt"
			+ "rbcjthztqfrxchxmcjcjlxqgjmshzkbswxemdlckfsydsglycjjssjnqbjctyhbftdcyjdgwyghqfrxwckqkxebpdjpx"
			+ "jqsrmebwgjlbjslyysmdxlclqkxlhtjrjjmbjhxhwywcbhtrxxglhjhfbmgykldyxzpplggpmtcbbajjzyljtyanjgbj"
			+ "flqgdzyqcaxbkclecjsznslyzhlxlzcghbxzhznytdsbcjkdlzayffydlabbgqszkggldndnyskjshdlxxbcghxyggdj"
			+ "mmzngmmccgwzszxsjbznmlzdthcqydbdllscddnlkjyhjsycjlkohqasdhnhcsgaehdaashtcplcpqybsdmpjlpcjaql"
			+ "cdhjjasprchngjnlhlyyqyhwzpnccgwwmzffjqqqqxxaclbhkdjxdgmmydjxzllsygxgkjrywzwyclzmcsjzldbndcfc"
			+ "xyhlschycjqppqagmnyxpfrkssbjlyxyjjglnscmhcwwmnzjjlhmhchsyppttxrycsxbyhcsmxjsxnbwgpxxtaybgajc"
			+ "xlypdccwqocwkccsbnhcpdyznbcyytyckskybsqkkytqqxfcwchcwkelcqbsqyjqcclmthsywhmktlkjlychwheqjhtj"
			+ "hppqpqscfymmcmgbmhglgsllysdllljpchmjhwljcyhzjxhdxjlhxrswlwzjcbxmhzqxsdzpmgfcsglsdymjshxpjxom"
			+ "yqknmyblrthbcftpmgyxlchlhlzylxgsssscclsldclepbhshxyyfhbmgdfycnjqwlqhjjcywjztejjdhfblqxtqkwhd"
			+ "chqxagtlxljxmsljhdzkzjecxjcjnmbbjcsfywkbjzghysdcpqyrsljpclpwxsdwejbjcbcnaytmgmbapclyqbclzxcb"
			+ "nmsggfnzjjbzsfqyndxhpcqkzczwalsbccjxpozgwkybsgxfcfcdkhjbstlqfsgdslqwzkxtmhsbgzhjcrglyjbpmljs"
			+ "xlcjqqhzmjczydjwbmjklddpmjegxyhylxhlqyqhkycwcjmyhxnatjhyccxzpcqlbzwwwtwbqcmlbmynjcccxbbsnzzl"
			+ "jpljxyztzlgcldcklyrzzgqtgjhhgjljaxfgfjzslcfdqzlclgjdjcsnclljpjqdcclcjxmyzftsxgcgsbrzxjqqcczh"
			+ "gyjdjqqlzxjyldlbcyamcstylbdjbyregklzdzhldszchznwczcllwjqjjjkdgjcolbbzppglghtgzcygezmycnqcycy"
			+ "hbhgxkamtxyxnbskyzzgjzlqjdfcjxdygjqjjpmgwgjjjpkjsbgbmmcjssclpqpdxcdyykypcjddyygywchjrtgcnyql"
			+ "dkljczzgzccjgdyksgpzmdlcphnjafyzdjcnmwescsglbtzcgmsdllyxqsxsbljsbbsgghfjlwpmzjnlyywdqshzxtyy"
			+ "whmcyhywdbxbtlmswyyfsbjcbdxxlhjhfpsxzqhfzmqcztqcxzxrdkdjhnnyzqqfnqdmmgnydxmjgdhcdycbffallztd"
			+ "ltfkmxqzdngeqdbdczjdxbzgsqqddjcmbkxffxmkdmcsychzcmljdjynhprsjmkmpcklgdbqtfzswtfgglyplljzhgjj"
			+ "gypzltcsmcnbtjbhfkdhbyzgkpbbymtdlsxsbnpdkleycjnycdykzddhqgsdzsctarlltkzlgecllkjljjaqnbdggghf"
			+ "jtzqjsecshalqfmmgjnlyjbbtmlycxdcjpldlpcqdhsycbzsckbzmsljflhrbjsnbrgjhxpdgdjybzgdlgcsezgxlblg"
			+ "yxtwmabchecmwyjyzlljjshlgndjlslygkdzpzxjyyzlpcxszfgwyydlyhcljscmbjhblyjlycblydpdqysxktbytdkd"
			+ "xjypcnrjmfdjgklccjbctbjddbblblcdqrppxjcglzcshltoljnmdddlngkaqakgjgyhheznmshrphqqjchgmfprxcjg"
			+ "dychghlyrzqlcngjnzsqdkqjymszswlcfqjqxgbggxmdjwlmcrnfkkfsyyljbmqammmycctbshcptxxzzsmphfshmclm"
			+ "ldjfyqxsdyjdjjzzhqpdszglssjbckbxyqzjsgpsxjzqznqtbdkwxjkhhgflbcsmdldgdzdblzkycqnncsybzbfglzzx"
			+ "swmsccmqnjqsbdqsjtxxmbldxcclzshzcxrqjgjylxzfjphymzqqydfqjjlcznzjcdgzygcdxmzysctlkphtxhtlbjxj"
			+ "lxscdqccbbqjfqzfsltjbtkqbsxjjljchczdbzjdczjccprnlqcgpfczlclcxzdmxmphgsgzgszzqjxlwtjpfsyaslcj"
			+ "btckwcwmytcsjjljcqlwzmalbxyfbpnlschtgjwejjxxglljstgshjqlzfkcgnndszfdeqfhbsaqdgylbxmmygszldyd"
			+ "jmjjrgbjgkgdhgkblgkbdmbylxwcxyttybkmrjjzxqjbhlmhmjjzmqasldcyxyqdlqcafywyxqhz";

		//返回给定字符串的首字母
		public static String IndexCode(String IndexTxt)
		{
            try
            {
                String _Temp = null;
                for (int i = 0; i < IndexTxt.Length; i++)
                    _Temp = _Temp + GetOneIndex(IndexTxt.Substring(i, 1));
                return _Temp.ToLower();
            }
            catch 
            {
                return "";
            }
		}
        
		//得到单个字符的首字母
		private static String GetOneIndex(String OneIndexTxt)
		{
			if(Convert.ToChar(OneIndexTxt)>=0 && Convert.ToChar(OneIndexTxt)<256)
				return OneIndexTxt;
			else
			{
				Encoding gb2312 = Encoding.GetEncoding("gb2312");
				byte[] unicodeBytes = Encoding.Unicode.GetBytes(OneIndexTxt);
				byte[] gb2312Bytes = Encoding.Convert(Encoding.Unicode, gb2312, unicodeBytes);
				return GetX(Convert.ToInt32(
					String.Format("{0:D2}",Convert.ToInt16(gb2312Bytes[0])-160)
					+ String.Format("{0:D2}",Convert.ToInt16(gb2312Bytes[1])-160)
					));
			}

		}


		//根据区位得到首字母
		private static String GetX(int GBCode)
		{
			if(GBCode >= 1601 && GBCode < 1637)return "A";
			if(GBCode >= 1637 && GBCode < 1833)return "B";
			if(GBCode >= 1833 && GBCode < 2078)return "C";
			if(GBCode >= 2078 && GBCode < 2274)return "D";
			if(GBCode >= 2274 && GBCode < 2302)return "E";
			if(GBCode >= 2302 && GBCode < 2433)return "F";
			if(GBCode >= 2433 && GBCode < 2594)return "G";
			if(GBCode >= 2594 && GBCode < 2787)return "H";
			if(GBCode >= 2787 && GBCode < 3106)return "J";
			if(GBCode >= 3106 && GBCode < 3212)return "K";
			if(GBCode >= 3212 && GBCode < 3472)return "L";
			if(GBCode >= 3472 && GBCode < 3635)return "M";
			if(GBCode >= 3635 && GBCode < 3722)return "N";
			if(GBCode >= 3722 && GBCode < 3730)return "O";
			if(GBCode >= 3730 && GBCode < 3858)return "P";
			if(GBCode >= 3858 && GBCode < 4027)return "Q";
			if(GBCode >= 4027 && GBCode < 4086)return "R";
			if(GBCode >= 4086 && GBCode < 4390)return "S";
			if(GBCode >= 4390 && GBCode < 4558)return "T";
			if(GBCode >= 4558 && GBCode < 4684)return "W";
			if(GBCode >= 4684 && GBCode < 4925)return "X";
			if(GBCode >= 4925 && GBCode < 5249)return "Y";
			if(GBCode >= 5249 && GBCode <= 5589)return "Z";
			if(GBCode >= 5601 && GBCode <= 8794)
			{			
				
				String _gbcode = GBCode.ToString();
				int pos = (Convert.ToInt16(_gbcode.Substring(0,2))-56)*94+Convert.ToInt16(_gbcode.Substring(_gbcode.Length-2,2));
				return CodeData.Substring(pos-1,1);
			}
			return " ";
		}
	}
}
