using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CFramework
{
    public class BinaryObjectAttribute : Attribute
    {
        /// <summary>
        /// 绑定了此标签的public非static const参数，才会参与序列化过程
        /// </summary>
        /// <param name="index">从小到大依次序列化，不要有相同的index存在，建议从0或1开始自增</param>
        public BinaryObjectAttribute ( int index )
        {
            Index = index;
        }

        public int Index;
    }
    /// <summary>
    /// 自动把相关类转为二进制，压缩比是Protobuf的一半，性能比protobuf慢一倍(35w List数据要关600ms 300的900)
    /// 支持类型: byte sbyte ushort short uint int ulong long float double string，
    /// 以及以基础类型为字段的子类，
    /// 还包括以上类型为基础的List和Array（不支持item为基础类型，如果要使用请把基础类型包装到一个类或结构体内）;
    /// string长度最多不超过ushort.MaxValue;
    /// List和数组长度最多不超过int.MaxValue;
    /// Dictionary的长度最多不超过int.MaxValue;
    /// </summary>
    public class BinaryObjectMapper
    {
        public static void Dispose ()
        {
            _cachedTypeMethods.Clear ();
        }

        /// <summary>
        /// 序列化的类
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static byte[] Serialize ( object target )
        {
            byte[] SerializeItem ( Type type, object targetObj )
            {
                var fields = GetMethods ( type );
                try
                {
                    using ( var ms = new MemoryStream () )
                    {
                        T GetValue<T> ( FieldInfo cf )
                        {
                            return (T) cf.GetValue ( targetObj );
                        }

                        void WriteIt ( byte[] bytes )
                        {
                            ms.Write ( bytes, 0, bytes.Length );
                        }

                        foreach ( FieldInfo field in fields )
                        {
                            var fieldType = field.FieldType;
                            switch ( true )
                            {
                                case true when fieldType == typeof( int ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<int> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( string ):
                                    byte[] sb = Encoding.UTF8.GetBytes ( GetValue<string> ( field ) );
                                    WriteIt ( BitConverter.GetBytes ( (ushort) sb.Length ) ); //先写2个字节长度
                                    WriteIt ( sb );
                                    break;

                                case true when fieldType == typeof( float ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<float> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( double ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<double> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( sbyte ):
                                    WriteIt ( new[] { (byte) GetValue<sbyte> ( field ) } );
                                    break;

                                case true when fieldType == typeof( byte ):
                                    WriteIt ( new[] { GetValue<byte> ( field ) } );
                                    break;

                                case true when fieldType == typeof( ushort ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<ushort> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( short ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<short> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( uint ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<uint> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( ulong ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<ulong> ( field ) ) );
                                    break;

                                case true when fieldType == typeof( long ):
                                    WriteIt ( BitConverter.GetBytes ( GetValue<long> ( field ) ) );
                                    break;

                                //Array List<>
                                case true when fieldType.IsArray:
                                case true when fieldType.IsGenericType
                                               && fieldType.GetGenericTypeDefinition () == typeof( List<> ):
                                {
                                    IList targetList = GetValue<IList> ( field );
                                    int   count      = targetList.Count; //先写4个字节长度
                                    WriteIt ( BitConverter.GetBytes ( count ) );
                                    if ( count > 0 )
                                    {
                                        Type itemType = fieldType.IsArray
                                                            ? fieldType.GetElementType ()
                                                            : fieldType.GetGenericArguments ()[ 0 ];

                                        bool isSimpleType = IsSimpleType ( itemType );

                                        for ( int i = 0; i < count; i++ )
                                        {
                                            if ( isSimpleType )
                                            {
                                                byte[] simpleBytes = SerializeSimpleValue ( targetList[ i ] );
                                                WriteIt ( simpleBytes );
                                            }
                                            else
                                            {
                                                byte[] childItemBytes = SerializeItem ( itemType, targetList[ i ] );
                                                WriteIt ( childItemBytes );
                                            }
                                        }
                                    }

                                    break;
                                }

                                //Dictionary
                                case true when fieldType.IsGenericType
                                               && fieldType.GetGenericTypeDefinition () == typeof( Dictionary<,> ):
                                {
                                    IDictionary targetDict = GetValue<IDictionary> ( field );
                                    int         count      = targetDict.Count; //先写4个字节长度
                                    WriteIt ( BitConverter.GetBytes ( count ) );
                                    if ( count > 0 )
                                    {
                                        var dictKeyType   = fieldType.GetGenericArguments ()[ 0 ];
                                        var dictValueType = fieldType.GetGenericArguments ()[ 1 ];

                                        bool keySimple   = IsSimpleType ( dictKeyType );
                                        bool valueSimple = IsSimpleType ( dictValueType );

                                        foreach ( DictionaryEntry entry in targetDict )
                                        {
                                            if ( keySimple )
                                            {
                                                byte[] keyBytes = SerializeSimpleValue ( entry.Key );
                                                WriteIt ( keyBytes );
                                            }
                                            else
                                            {
                                                byte[] keyBytes = SerializeItem ( dictKeyType, entry.Key );
                                                WriteIt ( keyBytes );
                                            }

                                            if ( valueSimple )
                                            {
                                                byte[] valueBytes = SerializeSimpleValue ( entry.Value );
                                                WriteIt ( valueBytes );
                                            }
                                            else
                                            {
                                                byte[] valueBytes = SerializeItem ( dictValueType, entry.Value );
                                                WriteIt ( valueBytes );
                                            }
                                        }
                                    }

                                    break;
                                }

                                default:
                                    byte[] childBytes = SerializeItem ( fieldType, GetValue<object> ( field ) );
                                    WriteIt ( childBytes );
                                    break;
                            }
                        }

                        return ms.ToArray ();
                    }
                }

                catch ( Exception e )
                {
                    Console.WriteLine ( $"序列化类:{type}失败 msg:{e.Message} \nstack:{e.StackTrace}" );
                    return null;
                }
            }

            return SerializeItem ( target.GetType (), target );
        }

        /// <summary>
        /// 把简单对象序列化
        /// </summary>
        private static byte[] SerializeSimpleValue ( object simpleValue )
        {
            switch ( simpleValue )
            {
                case int i: return BitConverter.GetBytes ( i );

                case string str:
                    byte[] sb    = Encoding.UTF8.GetBytes ( str );
                    ushort len   = (ushort) sb.Length;
                    byte[] lenBa = BitConverter.GetBytes ( len );
                    byte[] ba    = new byte[ len + 2 ];
                    Buffer.BlockCopy ( lenBa, 0, ba, 0, lenBa.Length );
                    Buffer.BlockCopy ( sb, 0, ba, lenBa.Length, sb.Length );
                    return ba;

                case float flt: return BitConverter.GetBytes ( flt );

                case double dbl: return BitConverter.GetBytes ( dbl );

                case sbyte sbt: return new[] { (byte) sbt };

                case byte bt: return new[] { bt };

                case ushort ust: return BitConverter.GetBytes ( ust );

                case short st: return BitConverter.GetBytes ( st );

                case uint ui: return BitConverter.GetBytes ( ui );

                case ulong ulg: return BitConverter.GetBytes ( ulg );

                case long lg: return BitConverter.GetBytes ( lg );

                default:
                    throw new Exception ( $"不支持的简单类型: {simpleValue.GetType ()}" );
            }
        }

        /// <summary>
        /// 反序列化的类应该与序列化的类结构一致
        /// </summary>
        /// <param name="bytes"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Deserialize<T> ( byte[] bytes ) where T : class, new ()
        {
            var type      = typeof( T );
            var classInst = Activator.CreateInstance<T> ();
            using ( var targetMS = new MemoryStream ( bytes ) )
            {
                object DeserializeItem ( Type targetType, object inst, MemoryStream ms )
                {
                    try
                    {
                        void SetValue ( FieldInfo cf, object value )
                        {
                            cf.SetValue ( inst, value );
                        }

                        var fields = GetMethods ( targetType );
                        foreach ( FieldInfo field in fields )
                        {
                            var fieldType = field.FieldType;
                            switch ( true )
                            {
                                case true when fieldType == typeof( int ):
                                    ms.Read ( TempCacheBytes, 0, 4 );
                                    SetValue ( field, BitConverter.ToInt32 ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( string ):
                                    ms.Read ( TempCacheBytes, 0, 2 );
                                    ushort len = BitConverter.ToUInt16 ( TempCacheBytes, 0 );
                                    ms.Read ( TempCacheBytes, 0, len );
                                    SetValue ( field, Encoding.UTF8.GetString ( TempCacheBytes, 0, len ) );
                                    break;

                                case true when fieldType == typeof( float ):
                                    ms.Read ( TempCacheBytes, 0, 4 );
                                    SetValue ( field, BitConverter.ToSingle ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( double ):
                                    ms.Read ( TempCacheBytes, 0, 8 );
                                    SetValue ( field, BitConverter.ToDouble ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( sbyte ):
                                    ms.Read ( TempCacheBytes, 0, 1 );
                                    SetValue ( field, (sbyte) TempCacheBytes[ 0 ] );
                                    break;

                                case true when fieldType == typeof( byte ):
                                    ms.Read ( TempCacheBytes, 0, 1 );
                                    SetValue ( field, TempCacheBytes[ 0 ] );
                                    break;

                                case true when fieldType == typeof( ushort ):
                                    ms.Read ( TempCacheBytes, 0, 2 );
                                    SetValue ( field, BitConverter.ToUInt16 ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( short ):
                                    ms.Read ( TempCacheBytes, 0, 2 );
                                    SetValue ( field, BitConverter.ToInt16 ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( uint ):
                                    ms.Read ( TempCacheBytes, 0, 4 );
                                    SetValue ( field, BitConverter.ToUInt32 ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( ulong ):
                                    ms.Read ( TempCacheBytes, 0, 8 );
                                    SetValue ( field, BitConverter.ToUInt64 ( TempCacheBytes, 0 ) );
                                    break;

                                case true when fieldType == typeof( long ):
                                    ms.Read ( TempCacheBytes, 0, 8 );
                                    SetValue ( field, BitConverter.ToInt64 ( TempCacheBytes, 0 ) );
                                    break;

                                //Array List<>
                                case true when fieldType.IsArray:
                                case true when fieldType.IsGenericType
                                               && fieldType.GetGenericTypeDefinition () == typeof( List<> ):
                                {
                                    ms.Read ( TempCacheBytes, 0, 4 );
                                    int count = BitConverter.ToInt32 ( TempCacheBytes, 0 );

                                    IList listInst;
                                    Type  itemType;
                                    bool  isArray = fieldType.IsArray;
                                    if ( isArray )
                                    {
                                        listInst = (IList) Activator.CreateInstance ( fieldType, count );
                                        itemType = fieldType.GetElementType ();
                                    }
                                    else
                                    {
                                        listInst = (IList) Activator.CreateInstance ( fieldType );
                                        itemType = fieldType.GetGenericArguments ()[ 0 ];
                                    }

                                    bool isSimpleType = IsSimpleType ( itemType );

                                    for ( int i = 0; i < count; i++ )
                                    {
                                        if ( isSimpleType )
                                        {
                                            var simpleValue =
                                                DeserializeSimpleValue ( itemType, ms, TempCacheBytes );
                                            if ( isArray )
                                            {
                                                listInst[ i ] = simpleValue;
                                            }
                                            else
                                            {
                                                listInst.Add ( simpleValue );
                                            }
                                        }
                                        else
                                        {
                                            var itemInst = Activator.CreateInstance ( itemType );
                                            DeserializeItem ( itemType, itemInst, ms );
                                            if ( isArray )
                                            {
                                                listInst[ i ] = itemInst;
                                            }
                                            else
                                            {
                                                listInst.Add ( itemInst );
                                            }
                                        }
                                    }

                                    SetValue ( field, listInst );

                                    break;
                                }

                                //Dictionary
                                case true when fieldType.IsGenericType
                                               && fieldType.GetGenericTypeDefinition () == typeof( Dictionary<,> ):
                                {
                                    ms.Read ( TempCacheBytes, 0, 4 );
                                    int count = BitConverter.ToInt32 ( TempCacheBytes, 0 );

                                    IDictionary dictInst      = (IDictionary) Activator.CreateInstance ( fieldType );
                                    var         dictKeyType   = fieldType.GetGenericArguments ()[ 0 ];
                                    var         dictValueType = fieldType.GetGenericArguments ()[ 1 ];

                                    bool keySimple   = IsSimpleType ( dictKeyType );
                                    bool valueSimple = IsSimpleType ( dictValueType );

                                    //这里的顺序是按照序列化时的foreach顺序
                                    for ( int i = 0; i < count; i++ )
                                    {
                                        object key;
                                        if ( keySimple )
                                        {
                                            key = DeserializeSimpleValue ( dictKeyType, ms, TempCacheBytes );
                                        }
                                        else
                                        {
                                            key = Activator.CreateInstance ( dictKeyType );
                                            DeserializeItem ( dictKeyType, key, ms );
                                        }

                                        object value;
                                        if ( valueSimple )
                                        {
                                            value = DeserializeSimpleValue ( dictValueType, ms, TempCacheBytes );
                                        }
                                        else
                                        {
                                            value = Activator.CreateInstance ( dictValueType );
                                            DeserializeItem ( dictValueType, value, ms );
                                        }

                                        dictInst.Add ( key, value );
                                    }

                                    SetValue ( field, dictInst );
                                    break;
                                }

                                default:
                                    var childInst = Activator.CreateInstance ( fieldType );
                                    DeserializeItem ( fieldType, childInst, ms );
                                    SetValue ( field, childInst );
                                    break;
                            }
                        }

                        return inst;
                    }
                    catch ( Exception e )
                    {
                        Console.WriteLine ( $"反序列化类:{type}失败, msg:{e.Message} \nstack:{e.StackTrace}" );
                        return null;
                    }
                }

                return (T) DeserializeItem ( type, classInst, targetMS );
            }
        }

        /// <summary>
        /// 把字节流转为简单对象
        /// </summary>
        private static object DeserializeSimpleValue ( Type type, MemoryStream ms, byte[] dest )
        {
            switch ( true )
            {
                case true when type == typeof( int ):
                    ms.Read ( dest, 0, 4 );
                    return BitConverter.ToInt32 ( dest, 0 );

                case true when type == typeof( string ):
                    ms.Read ( dest, 0, 2 );
                    ushort len = BitConverter.ToUInt16 ( dest, 0 );
                    ms.Read ( dest, 0, len );
                    return Encoding.UTF8.GetString ( dest, 0, len );

                case true when type == typeof( float ):
                    ms.Read ( dest, 0, 4 );
                    return BitConverter.ToSingle ( dest, 0 );

                case true when type == typeof( double ):
                    ms.Read ( dest, 0, 8 );
                    return BitConverter.ToDouble ( dest, 0 );

                case true when type == typeof( sbyte ):
                    ms.Read ( dest, 0, 1 );
                    return (sbyte) dest[ 0 ];

                case true when type == typeof( byte ):
                    ms.Read ( dest, 0, 1 );
                    return dest[ 0 ];

                case true when type == typeof( ushort ):
                    ms.Read ( dest, 0, 2 );
                    return BitConverter.ToUInt16 ( dest, 0 );

                case true when type == typeof( short ):
                    ms.Read ( dest, 0, 2 );
                    return BitConverter.ToInt16 ( dest, 0 );

                case true when type == typeof( uint ):
                    ms.Read ( dest, 0, 4 );
                    return BitConverter.ToUInt32 ( dest, 0 );

                case true when type == typeof( ulong ):
                    ms.Read ( dest, 0, 8 );
                    return BitConverter.ToUInt64 ( dest, 0 );

                case true when type == typeof( long ):
                    ms.Read ( dest, 0, 8 );
                    return BitConverter.ToInt64 ( dest, 0 );

                default:
                    throw new Exception ( $"不支持的简单类型: {type}" );
            }
        }

        /// <summary>
        /// 自动排序
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static FieldInfo[] GetMethods ( Type type )
        {
            if ( _cachedTypeMethods.TryGetValue ( type, out var infos ) )
            {
                return infos;
            }

            infos = type.GetFields ( BindingFlags.Public | BindingFlags.Instance );
            List<FieldInfoData> relatedInfos = new List<FieldInfoData> ();
            foreach ( FieldInfo info in infos )
            {
                BinaryObjectAttribute attr;
                if ( ( attr = info.GetCustomAttribute<BinaryObjectAttribute> () ) == null )
                {
                    continue;
                }

                relatedInfos.Add ( new FieldInfoData
                {
                    Info  = info,
                    Index = attr.Index
                } );
            }

            relatedInfos.Sort ( ( i1, i2 ) =>
            {
                if ( i1.Index < i2.Index )
                {
                    return -1;
                }

                return i2.Index > i1.Index ? 1 : 0;
            } );

            FieldInfo[] result = ( from infoData in relatedInfos select infoData.Info ).ToArray ();
            _cachedTypeMethods.Add ( type, result );
            return result;
        }

        /// <summary>
        /// 如果是简单类型，就不用去找是否带[BinaryObjectAttribute]标签，直接做简单的处理，
        /// Array List Dictionary检测时会使用到
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsSimpleType ( Type type )
        {
            return SIMPLE_TYPES.Contains ( type );
        }

        private static readonly List<Type> SIMPLE_TYPES = new List<Type>
        {
            typeof( int ),
            typeof( string ),
            typeof( float ),
            typeof( double ),
            typeof( sbyte ),
            typeof( byte ),
            typeof( ushort ),
            typeof( short ),
            typeof( uint ),
            typeof( ulong ),
            typeof( long ),
        };

        private static readonly Dictionary<Type, FieldInfo[]> _cachedTypeMethods = new Dictionary<Type, FieldInfo[]> ();

        private static readonly byte[] TempCacheBytes = new byte[ 1024 ];

        private struct FieldInfoData
        {
            public FieldInfo Info;

            public int Index;
        }
    }
}