# BinaryObjectMapper
C#类的序列化

#支持的类
在需要序列化的public field上加个[BinaryObject]，即可以序列化此条，[BinaryObject(n)]，n代表序列化顺序。序列化类可以和反序列化类不一样，只需要保证字段类型和序列化顺序一致就可以，比如

``
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
``

# 与Protobuf-net比较
序列化后的流比protofub小50%，在执行List数量不多的情况下，效率比protobuf快1/3。
当执行量级为35w条时，效率比protobuf慢一倍，这个后续再优化