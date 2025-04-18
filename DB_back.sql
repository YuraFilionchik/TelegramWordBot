PGDMP  (                    }           postgres    16.8 (Debian 16.8-1.pgdg120+1)    17.4 *    O           0    0    ENCODING    ENCODING        SET client_encoding = 'UTF8';
                           false            P           0    0 
   STDSTRINGS 
   STDSTRINGS     (   SET standard_conforming_strings = 'on';
                           false            Q           0    0 
   SEARCHPATH 
   SEARCHPATH     8   SELECT pg_catalog.set_config('search_path', '', false);
                           false            R           1262    5    postgres    DATABASE     s   CREATE DATABASE postgres WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'en_US.utf8';
    DROP DATABASE postgres;
                     postgres    false            S           0    0    DATABASE postgres    COMMENT     N   COMMENT ON DATABASE postgres IS 'default administrative connection database';
                        postgres    false    3410            �            1259    16433 	   languages    TABLE     k   CREATE TABLE public.languages (
    id integer NOT NULL,
    code text NOT NULL,
    name text NOT NULL
);
    DROP TABLE public.languages;
       public         heap r       postgres    false            �            1259    16432    languages_id_seq    SEQUENCE     �   CREATE SEQUENCE public.languages_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 '   DROP SEQUENCE public.languages_id_seq;
       public               postgres    false    217            T           0    0    languages_id_seq    SEQUENCE OWNED BY     E   ALTER SEQUENCE public.languages_id_seq OWNED BY public.languages.id;
          public               postgres    false    216            �            1259    16455    translations    TABLE     �   CREATE TABLE public.translations (
    id uuid NOT NULL,
    word_id uuid NOT NULL,
    language_id integer NOT NULL,
    text text NOT NULL
);
     DROP TABLE public.translations;
       public         heap r       postgres    false            �            1259    16508    user_languages    TABLE     d   CREATE TABLE public.user_languages (
    user_id uuid NOT NULL,
    language_id integer NOT NULL
);
 "   DROP TABLE public.user_languages;
       public         heap r       postgres    false            �            1259    16472    user_word_progress    TABLE     1  CREATE TABLE public.user_word_progress (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    word_id uuid NOT NULL,
    last_review timestamp without time zone,
    count_total_view integer DEFAULT 0,
    count_plus integer DEFAULT 0,
    count_minus integer DEFAULT 0,
    progress integer DEFAULT 0
);
 &   DROP TABLE public.user_word_progress;
       public         heap r       postgres    false            �            1259    16493 
   user_words    TABLE     Y   CREATE TABLE public.user_words (
    user_id uuid NOT NULL,
    word_id uuid NOT NULL
);
    DROP TABLE public.user_words;
       public         heap r       postgres    false            �            1259    16423    users    TABLE     o   CREATE TABLE public.users (
    id uuid NOT NULL,
    telegram_id bigint NOT NULL,
    native_language text
);
    DROP TABLE public.users;
       public         heap r       postgres    false            �            1259    16443    words    TABLE     s   CREATE TABLE public.words (
    id uuid NOT NULL,
    base_text text NOT NULL,
    language_id integer NOT NULL
);
    DROP TABLE public.words;
       public         heap r       postgres    false            �           2604    16436    languages id    DEFAULT     l   ALTER TABLE ONLY public.languages ALTER COLUMN id SET DEFAULT nextval('public.languages_id_seq'::regclass);
 ;   ALTER TABLE public.languages ALTER COLUMN id DROP DEFAULT;
       public               postgres    false    217    216    217            G          0    16433 	   languages 
   TABLE DATA           3   COPY public.languages (id, code, name) FROM stdin;
    public               postgres    false    217   3       I          0    16455    translations 
   TABLE DATA           F   COPY public.translations (id, word_id, language_id, text) FROM stdin;
    public               postgres    false    219   �5       L          0    16508    user_languages 
   TABLE DATA           >   COPY public.user_languages (user_id, language_id) FROM stdin;
    public               postgres    false    222   �5       J          0    16472    user_word_progress 
   TABLE DATA           �   COPY public.user_word_progress (id, user_id, word_id, last_review, count_total_view, count_plus, count_minus, progress) FROM stdin;
    public               postgres    false    220   �5       K          0    16493 
   user_words 
   TABLE DATA           6   COPY public.user_words (user_id, word_id) FROM stdin;
    public               postgres    false    221   
6       E          0    16423    users 
   TABLE DATA           A   COPY public.users (id, telegram_id, native_language) FROM stdin;
    public               postgres    false    215   '6       H          0    16443    words 
   TABLE DATA           ;   COPY public.words (id, base_text, language_id) FROM stdin;
    public               postgres    false    218   D6       U           0    0    languages_id_seq    SEQUENCE SET     ?   SELECT pg_catalog.setval('public.languages_id_seq', 59, true);
          public               postgres    false    216            �           2606    16442    languages languages_code_key 
   CONSTRAINT     W   ALTER TABLE ONLY public.languages
    ADD CONSTRAINT languages_code_key UNIQUE (code);
 F   ALTER TABLE ONLY public.languages DROP CONSTRAINT languages_code_key;
       public                 postgres    false    217            �           2606    16440    languages languages_pkey 
   CONSTRAINT     V   ALTER TABLE ONLY public.languages
    ADD CONSTRAINT languages_pkey PRIMARY KEY (id);
 B   ALTER TABLE ONLY public.languages DROP CONSTRAINT languages_pkey;
       public                 postgres    false    217            �           2606    16461    translations translations_pkey 
   CONSTRAINT     \   ALTER TABLE ONLY public.translations
    ADD CONSTRAINT translations_pkey PRIMARY KEY (id);
 H   ALTER TABLE ONLY public.translations DROP CONSTRAINT translations_pkey;
       public                 postgres    false    219            �           2606    16512 "   user_languages user_languages_pkey 
   CONSTRAINT     r   ALTER TABLE ONLY public.user_languages
    ADD CONSTRAINT user_languages_pkey PRIMARY KEY (user_id, language_id);
 L   ALTER TABLE ONLY public.user_languages DROP CONSTRAINT user_languages_pkey;
       public                 postgres    false    222    222            �           2606    16480 *   user_word_progress user_word_progress_pkey 
   CONSTRAINT     h   ALTER TABLE ONLY public.user_word_progress
    ADD CONSTRAINT user_word_progress_pkey PRIMARY KEY (id);
 T   ALTER TABLE ONLY public.user_word_progress DROP CONSTRAINT user_word_progress_pkey;
       public                 postgres    false    220            �           2606    16482 9   user_word_progress user_word_progress_user_id_word_id_key 
   CONSTRAINT     �   ALTER TABLE ONLY public.user_word_progress
    ADD CONSTRAINT user_word_progress_user_id_word_id_key UNIQUE (user_id, word_id);
 c   ALTER TABLE ONLY public.user_word_progress DROP CONSTRAINT user_word_progress_user_id_word_id_key;
       public                 postgres    false    220    220            �           2606    16497    user_words user_words_pkey 
   CONSTRAINT     f   ALTER TABLE ONLY public.user_words
    ADD CONSTRAINT user_words_pkey PRIMARY KEY (user_id, word_id);
 D   ALTER TABLE ONLY public.user_words DROP CONSTRAINT user_words_pkey;
       public                 postgres    false    221    221            �           2606    16429    users users_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.users DROP CONSTRAINT users_pkey;
       public                 postgres    false    215            �           2606    16431    users users_telegram_id_key 
   CONSTRAINT     ]   ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_telegram_id_key UNIQUE (telegram_id);
 E   ALTER TABLE ONLY public.users DROP CONSTRAINT users_telegram_id_key;
       public                 postgres    false    215            �           2606    16449    words words_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.words
    ADD CONSTRAINT words_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.words DROP CONSTRAINT words_pkey;
       public                 postgres    false    218            �           2606    16467 *   translations translations_language_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.translations
    ADD CONSTRAINT translations_language_id_fkey FOREIGN KEY (language_id) REFERENCES public.languages(id);
 T   ALTER TABLE ONLY public.translations DROP CONSTRAINT translations_language_id_fkey;
       public               postgres    false    3232    219    217            �           2606    16462 &   translations translations_word_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.translations
    ADD CONSTRAINT translations_word_id_fkey FOREIGN KEY (word_id) REFERENCES public.words(id) ON DELETE CASCADE;
 P   ALTER TABLE ONLY public.translations DROP CONSTRAINT translations_word_id_fkey;
       public               postgres    false    219    218    3234            �           2606    16518 .   user_languages user_languages_language_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_languages
    ADD CONSTRAINT user_languages_language_id_fkey FOREIGN KEY (language_id) REFERENCES public.languages(id);
 X   ALTER TABLE ONLY public.user_languages DROP CONSTRAINT user_languages_language_id_fkey;
       public               postgres    false    3232    217    222            �           2606    16513 *   user_languages user_languages_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_languages
    ADD CONSTRAINT user_languages_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 T   ALTER TABLE ONLY public.user_languages DROP CONSTRAINT user_languages_user_id_fkey;
       public               postgres    false    3226    222    215            �           2606    16483 2   user_word_progress user_word_progress_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_word_progress
    ADD CONSTRAINT user_word_progress_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 \   ALTER TABLE ONLY public.user_word_progress DROP CONSTRAINT user_word_progress_user_id_fkey;
       public               postgres    false    3226    220    215            �           2606    16488 2   user_word_progress user_word_progress_word_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_word_progress
    ADD CONSTRAINT user_word_progress_word_id_fkey FOREIGN KEY (word_id) REFERENCES public.words(id) ON DELETE CASCADE;
 \   ALTER TABLE ONLY public.user_word_progress DROP CONSTRAINT user_word_progress_word_id_fkey;
       public               postgres    false    218    220    3234            �           2606    16498 "   user_words user_words_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_words
    ADD CONSTRAINT user_words_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 L   ALTER TABLE ONLY public.user_words DROP CONSTRAINT user_words_user_id_fkey;
       public               postgres    false    3226    215    221            �           2606    16503 "   user_words user_words_word_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_words
    ADD CONSTRAINT user_words_word_id_fkey FOREIGN KEY (word_id) REFERENCES public.words(id) ON DELETE CASCADE;
 L   ALTER TABLE ONLY public.user_words DROP CONSTRAINT user_words_word_id_fkey;
       public               postgres    false    218    3234    221            �           2606    16450    words words_language_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.words
    ADD CONSTRAINT words_language_id_fkey FOREIGN KEY (language_id) REFERENCES public.languages(id);
 F   ALTER TABLE ONLY public.words DROP CONSTRAINT words_language_id_fkey;
       public               postgres    false    217    3232    218            G   $  x�-�ˎ�0E�WSĖ��2�Ng�&i��бb+��)-%H���Ӆ�.�C^f�����Ш����v�J�qxq�r���~�E��f*�^�q`<���F-PY<[�ɫ%���$c�>#��T�1�f���4N�Y������:�������@eǓ:�
em�k`;���]�v���;�I�-pr��l��O�K�o���B$�w��gH��\%�gp�(R>�N�o��H-~�L�O���C�2w^� Ė�ir�h�kȩ|�:�9Ƕ.�z���T��W�;!��"�Wz���Q9R:C�]�	]��؞�q��q	�8�"4p56u�Wz���Xl�s��L���� ���z��ʺ;�4����rI�("ef����V�M�KwxT�9	��ƽ������=��Ó�ӕ��ŋ-ٞ���U��*��S�|���g[O���v`f��Ŷ'jU1CKrV�'a!8���N�9���]K�Uh���L'w�W�AM��t���(���9��7�t�K���xb��K�����bW+v�mu���E)��K�K      I      x������ � �      L      x������ � �      J      x������ � �      K      x������ � �      E      x������ � �      H      x������ � �     