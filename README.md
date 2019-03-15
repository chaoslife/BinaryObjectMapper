# BinaryObjectMapper
C#类的序列化与反序列化工具类，需要C#7.3环境  
使用测试工程时，请把original.xml复制到/bin/debug/内，才可以加载成功  

# 使用说明
在需要序列化的public field上加个[BinaryObject]，即可以序列化此条，[BinaryObject(n)]，n代表序列化顺序。  
序列化类可以和反序列化类不一样，只需要保证字段类型和序列化顺序一致就可以，比如  
```
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
```

# 支持的类
可序列化:  
基础类型: bool, byte sbyte ushort short uint int ulong long float double string  
复杂类型: Array List Dictionary  
```
public string[] a;

public MyClass[] b;

public List<int> c;

public List<MyClass> d;

public Dictionary<string, int> e;

public Dictionary<MyClassA, MyClassB> f;
```

# 序列化复杂的对象
```
public class SomeObject
{
    [BinaryObject ( 1 )]
    public int id;

    [BinaryObject ( 2 )]
    public ItemObj obj;

    [BinaryObject ( 3 )]
    public Vector[] matrix;

    [BinaryObject ( 4 )]
    public Dictionary<string, ItemObj> otherItems;

    public class ItemObj
    {
        [BinaryObject ( 1 )]
        public int key;

        [BinaryObject ( 2 )]
        public Vector pos;

        [BinaryObject ( 3 )]
        public Vector size;
    }

    public class Vector
    {
        [BinaryObject ( 1 )]
        public int x;

        [BinaryObject ( 2 )]
        public int y;

        [BinaryObject ( 3 )]
        public int z;

        [BinaryObject ( 4 )]
        public List<int> all;
    }
}

byte[]   bytes = BinaryObjectMapper.Serialize ( new SomeObject () );
SomeObject cls = BinaryObjectMapper.Deserialize<SomeObject> ( bytes );
```

# 与Protobuf-net比较
序列化后的流比protofub小50%，在执行List数量不多的情况下，效率比protobuf快1/3。  
当执行量级为35w条时，效率比protobuf慢一倍，这个后续再优化。  
支持增量更新。  