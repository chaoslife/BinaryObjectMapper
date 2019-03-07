using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using CFramework;
using ProtoBuf;

namespace Test
{
    internal class Program
    {
        public static void Main ( string[] args )
        {
            Stopwatch stopwatch = new Stopwatch ();

            stopwatch.Start ();
            Console.WriteLine ( "------------ 加载XML源文件 ------------" );
            var xmlLoader = new XmlDocument ();
            xmlLoader.Load ( "original.xml" );
            stopwatch.Stop ();
            Console.WriteLine ( $"------------ 加载成功 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            var inFile       = new FileInfo ( "original.xml" );
            var inFileLength = inFile.Length;
            Console.WriteLine ( $"XML大小:{CountSize ( inFileLength )}  字节数:{inFileLength}" );

            stopwatch.Reset ();
            stopwatch.Start ();
            Console.WriteLine ( "------------ 解析XML为Class ------------" );
            var list   = xmlLoader.GetElementsByTagName ( "point" );
            var cls    = new XMLClass ();
            var proCls = new FromProtobufClass ();
            Console.WriteLine ( $"Items数量：{list.Count}" );
            for ( int i = 0; i < list.Count; i++ )
            {
                XmlElement item = (XmlElement) list[ i ];

                cls.items.Add ( new XMLClass.XMLItemClass
                {
                    id = Convert.ToInt32 ( item.GetAttribute ( "id" ).Trim () ),
                    x  = Convert.ToInt16 ( item.GetAttribute ( "x" ).Trim () ),
                    y  = Convert.ToInt16 ( item.GetAttribute ( "y" ).Trim () ),
                    z  = Convert.ToInt16 ( item.GetAttribute ( "z" ).Trim () ),
                    u  = Convert.ToSingle ( item.GetAttribute ( "u" ).Trim () ),
                    v  = Convert.ToSingle ( item.GetAttribute ( "v" ).Trim () ),
                    w  = Convert.ToSingle ( item.GetAttribute ( "w" ).Trim () ),
                } );
                proCls.items.Add ( new FromProtobufClass.ProtobufItemClass
                {
                    id = Convert.ToInt32 ( item.GetAttribute ( "id" ).Trim () ),
                    x  = Convert.ToInt16 ( item.GetAttribute ( "x" ).Trim () ),
                    y  = Convert.ToInt16 ( item.GetAttribute ( "y" ).Trim () ),
                    z  = Convert.ToInt16 ( item.GetAttribute ( "z" ).Trim () ),
                    u  = Convert.ToSingle ( item.GetAttribute ( "u" ).Trim () ),
                    v  = Convert.ToSingle ( item.GetAttribute ( "v" ).Trim () ),
                    w  = Convert.ToSingle ( item.GetAttribute ( "w" ).Trim () ),
                } );
            }

            stopwatch.Stop ();
            Console.WriteLine ( $"------------ 解析XML为Class 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            Console.WriteLine ( "------------ 序列化Class ------------" );
            stopwatch.Reset ();
            stopwatch.Start ();
            var clsBytes = BinaryObjectMapper.Serialize ( cls );
            stopwatch.Stop ();
            Console.WriteLine ( $"[使用Chaos码的] 序列化Class完成 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            File.WriteAllBytes ( "output.config", clsBytes );
            Console.WriteLine ( "------------ 保存二进制流为文件:output.config 成功 ------------" );

            var outFile    = new FileInfo ( "output.config" );
            var outFileLen = outFile.Length;

            Console.WriteLine ( $"[使用Chaos码的] 序列化后大小:{CountSize ( outFileLen )}  字节数:{outFileLen}" );
            Console.WriteLine ( $"[使用Chaos码的] 容量减少{(float) ( inFileLength - outFileLen ) / inFileLength:P}" );

            Console.WriteLine ( "------------ 序列化为Protobuf ------------" );
            stopwatch.Reset ();
            stopwatch.Start ();
            using ( var file = File.Create ( "output.bin" ) )
            {
                Serializer.Serialize ( file, proCls );
            }

            stopwatch.Stop ();
            Console.WriteLine ( $"[使用Protobuf] 序列化为Protobuf成功 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            var outProtoFile    = new FileInfo ( "output.bin" );
            var outProtoFileLen = outProtoFile.Length;
            Console.WriteLine ( $"[使用Protobuf] 序列化后大小:{CountSize ( outProtoFileLen )}  字节数:{outProtoFileLen}" );
            Console.WriteLine ( $"[使用Protobuf] 容量减少{(float) ( inFileLength - outProtoFileLen ) / inFileLength:P}" );

            Console.WriteLine ( "------------ 加载output.config文件 ------------" );
            var outBytes = File.ReadAllBytes ( "output.config" );

            Console.WriteLine ( "------------ 反序列化 ------------" );
            stopwatch.Reset ();
            stopwatch.Start ();
            var final = BinaryObjectMapper.Deserialize<FinalClass> ( outBytes );
            stopwatch.Stop ();
            Console.WriteLine ( $"[使用Chaos码的] 反序列化成功 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            Console.WriteLine ( "------------ 反序列化-Protobuf ------------" );
            stopwatch.Reset ();
            stopwatch.Start ();
            using ( var file = File.OpenRead ( "output.bin" ) )
            {
                var protobufOut = Serializer.Deserialize<FromProtobufClass> ( file );
            }

            stopwatch.Stop ();
            Console.WriteLine ( $"[使用Protobuf] 反序列化-Protobuf成功 花费:{stopwatch.ElapsedMilliseconds}毫秒 ------------" );

            try
            {
                Console.WriteLine ( "------------ 前10数据 ------------" );
                for ( int i = 0; i < 10; i++ )
                {
                    var item = final.items[ i ];
                    Console.WriteLine (
                        $"[FinalClass Item] id:{item.id} "
                        + $"x:{item.x} y:{item.y} z:{item.z} "
                        + $"u:{item.u:0.00000000} v:{item.v:0.00000000} w:{item.w:0.00000000}" );
                }
            }
            catch ( Exception )
            {
                //
            }

            while ( true )
            {
                Thread.Sleep ( 1 );
            }
        }

        /// <summary>
        /// 序列化的类
        /// </summary>
        public class XMLClass
        {
            [BinaryObject ( 1 )]
            public List<XMLItemClass> items = new List<XMLItemClass> ();

            public class XMLItemClass
            {
                [BinaryObject ( 1 )]
                public int id;

                [BinaryObject ( 2 )]
                public short x;

                [BinaryObject ( 3 )]
                public short y;

                [BinaryObject ( 4 )]
                public short z;

                [BinaryObject ( 5 )]
                public float u;

                [BinaryObject ( 6 )]
                public float v;

                [BinaryObject ( 7 )]
                public float w;
            }
        }

        public class From
        {
            [BinaryObject ( 1 )]
            public int id;

            [BinaryObject ( 2 )]
            public short x;

            [BinaryObject ( 3 )]
            public short y;

            [BinaryObject ( 4 )]
            public short z;
        }

        public class To
        {
            [BinaryObject ( 1 )]
            public int a;

            [BinaryObject ( 2 )]
            public short b;

            [BinaryObject ( 3 )]
            public short c;

            [BinaryObject ( 4 )]
            public short d;
        }

        /// <summary>
        /// 反出来的类
        /// </summary>
        public class FinalClass
        {
            [BinaryObject ( 1 )]
            public List<ItemFinailClass> items = new List<ItemFinailClass> ();

            public class ItemFinailClass
            {
                [BinaryObject ( 1 )]
                public int id;

                [BinaryObject ( 2 )]
                public short x;

                [BinaryObject ( 3 )]
                public short y;

                [BinaryObject ( 4 )]
                public short z;

                [BinaryObject ( 5 )]
                public float u;

                [BinaryObject ( 6 )]
                public float v;

                [BinaryObject ( 7 )]
                public float w;
            }
        }

        [ProtoContract]
        public class FromProtobufClass
        {
            [ProtoMember ( 1 )]
            public List<ProtobufItemClass> items = new List<ProtobufItemClass> ();

            [ProtoContract]
            public class ProtobufItemClass
            {
                [ProtoMember ( 1 )]
                public int id;

                [ProtoMember ( 2 )]
                public short x;

                [ProtoMember ( 3 )]
                public short y;

                [ProtoMember ( 4 )]
                public short z;

                [ProtoMember ( 5 )]
                public float u;

                [ProtoMember ( 6 )]
                public float v;

                [ProtoMember ( 7 )]
                public float w;
            }
        }

        private static string CountSize ( long Size )
        {
            string m_strSize = "";
            if ( Size < 1048576 ) /*
                m_strSize = ( Size / 1024.00 ).ToString ( "F2" ) + " K";
            else if ( Size >= 1024.00 && Size < 1048576 )*/
                m_strSize = ( Size / 1024.00 ).ToString ( "F1" ) + " KB";
            else if ( Size >= 1048576 && Size < 1073741824 )
                m_strSize = ( Size / 1024.00 / 1024.00 ).ToString ( "F1" ) + " MB";
            else if ( Size >= 1073741824 )
                m_strSize = ( Size / 1024.00 / 1024.00 / 1024.00 ).ToString ( "F1" ) + " GB";
            return m_strSize;
        }
    }
}