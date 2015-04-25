/* -----------------------------------------------------------------
 * Copyright (c) 2015 Robert Adams
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *
 *     * Redistributions in binary form must reproduce the above
 *       copyright notice, this list of conditions and the following
 *       disclaimer in the documentation and/or other materials provided
 *       with the distribution.
 *
 *     * Neither the name of the author nor the names of
 *       contributors may be used to endorse or promote products derived
 *       from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHOR OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * EXPORT LAWS: THIS LICENSE ADDS NO RESTRICTIONS TO THE EXPORT LAWS OF
 * YOUR JURISDICTION. It is licensee's responsibility to comply with any
 * export regulations applicable in licensee's jurisdiction. Under
 * CURRENT (May 2000) U.S. export regulations this software is eligible
 * for export from the U.S. and can be downloaded by or otherwise
 * exported or reexported worldwide EXCEPT to U.S. embargoed destinations
 * which include Cuba, Iraq, Libya, North Korea, Iran, Syria, Sudan,
 * Afghanistan and any other country to which the U.S. has embargoed
 * goods and services.
 * -----------------------------------------------------------------
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using MySql.Data.MySqlClient;

using ParameterParsing;
using Logging;

namespace importWP
{
class ImportWP
{
    Dictionary<string, string> m_Parameters;
    int m_verbose = 0;

    string m_dbHost;
    string m_dbName;
    string m_dbUser;
    string m_dbPass;

    string m_tablePrefix = "wp_";
    bool m_cleanEntities = false;
    
    private string Invocation()
    {
        return @"Invocation:
INVOCATION:
ImportWP 
        -H|--dbHost databaseHost
        -D|--dbName databaseName
        -U|--dbUser databaseUser
        -P|--dbPass databaseUserPassword
        -o|--output outputFilename
        --tablePrefix prefix
        --cleanEntities
        --verbose
";
    }

    static void Main(string[] args)
    {
        ImportWP prog = new ImportWP();
        prog.Start(args);
        return;
    }
    
    public ImportWP()
    {
    }

    public class PageName
    {
        public ulong ID;
        public string post_title;
        public string post_name;
        public ulong post_parent;

        public PageName()
        {
            ID = 0;
            post_title = String.Empty;
            post_name = String.Empty;
            post_parent = 0;
        }
    }

    public Dictionary<ulong, PageName> m_pageNameList = new Dictionary<ulong, PageName>();

    public class PostInfo
    {
        public ulong ID;
        public string guid;
        public string post_type;
        public string post_status;
        public string post_title;
        public string post_slug;
        public string post_name;
        public DateTime post_date;
        public DateTime post_date_gmt;
        public string post_content;
        public string post_excerpt;
        public int comment_count;
        public string display_name;
        public string user_login;
        public string user_email;
        public string user_url;

        public PostInfo()
        {
            ID = 0;
            guid = String.Empty;
            post_type = String.Empty;
            post_status = String.Empty;
            post_title = String.Empty;
            post_slug = String.Empty;
            post_name = String.Empty;
            post_date = DateTime.Now;
            post_date_gmt = DateTime.Now;
            post_content = String.Empty;
            post_excerpt = String.Empty;
            comment_count = 0;
            display_name = String.Empty;
            user_login = String.Empty;
            user_email = String.Empty;
            user_url = String.Empty;
        }
    }

    public Dictionary<ulong, PostInfo> m_posts = new Dictionary<ulong, PostInfo>();

    public class CommentInfo
    {
        public ulong comment_ID;
        public string author;
        public string author_email;
        public string author_url;
        public DateTime date;
        public DateTime date_gmt;
        public string content;

        public CommentInfo()
        {
            comment_ID = 0;
            author = String.Empty;
            author_email = String.Empty;
            author_url = String.Empty;
            date = DateTime.Now;
            date_gmt = DateTime.Now;
            content = String.Empty;
        }
    }

    public void Start(string[] args)
    {
        m_Parameters = ParameterParse.ParseArguments(args, false /* firstOpFlag */, true /* multipleFiles */);
        foreach (KeyValuePair<string, string> kvp in m_Parameters)
        {
            switch (kvp.Key)
            {
                case "-H":
                case "--dbHost":
                    m_dbHost = kvp.Value;
                    break;
                case "-D":
                case "--dbName":
                    m_dbName = kvp.Value;
                    break;
                case "-U":
                case "--dbUser":
                    m_dbUser = kvp.Value;
                    break;
                case "-P":
                case "--dbPass":
                    m_dbPass = kvp.Value;
                    break;
                case "--tablePrefix":
                    m_tablePrefix = kvp.Value;
                    break;
                case "--cleanEntities":
                    m_cleanEntities = true;
                    break;
                case "--verbose":
                    m_verbose++;
                    break;
                // case ParameterParse.LAST_PARAM:
                //     break;
                case ParameterParse.ERROR_PARAM:
                    // if we get here, the parser found an error
                    Logger.Log("Parameter error: " + kvp.Value);
                    Logger.Log(Invocation());
                    return;
                default:
                    Logger.Log("ERROR: UNKNOWN PARAMETER: " + kvp.Key);
                    Logger.Log(Invocation());
                    return;
            }
        }

        string m_connectionString = String.Format("Data Source={0};Database={1};User ID={2};Password={3}",
                                            m_dbHost, m_dbName, m_dbUser, m_dbPass);
        Logger.Log("Connection string = {0}", m_connectionString);

        Dictionary<ulong, Dictionary<string, string>> pageNameList = new Dictionary<ulong, Dictionary<string, string>>();
        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
        {
            dbcon.Open();

            // populate m_pageNameList
            GetPageNameList(dbcon);
            // populate m_posts
            GetPosts(dbcon);
            // Got through all the posts and output post files
            ProcessPosts(dbcon);
        }
    }

    private void ProcessPosts(MySqlConnection dbcon)
    {
        List<string> categories;
        List<string> tags;

        List<CommentInfo> comments;

        foreach (PostInfo post in m_posts.Values)
        {
            GetTagsAndCategoriesForPost(dbcon, post.ID, out categories, out tags);

            if (post.comment_count > 0)
                GetCommentsForPost(dbcon, post.ID, out comments);
            else
                comments = new List<CommentInfo>();

            var data = new
            {
                layout = post.post_type,
                status = post.post_status,
                published = post.post_status == "draft" ? String.Empty : "publish",
                title = post.post_title,
                author = new
                {
                    display_name = post.display_name,
                    login = post.user_login,
                    email = post.user_email,
                    url = post.user_url,
                },
                author_login = post.user_login,
                author_email = post.user_email,
                author_url = post.user_url,
                excerpt = post.post_excerpt,
                // more_anchor = post.more_anchor,
                // Seems like but in the Ruby code but I don't use More so not a problem
                // more_anchor = string.Empty,
                wordpress_id = post.ID.ToString(),
                wordpress_url = post.guid,
                date = post.post_date.ToString(),
                // date_gmt = post.post_date_gmt.ToString(),
                categories = categories,
                tags = tags,
                comments = comments
            };

            string filename = String.Empty;
            if (post.post_type == "page")
            {
                filename = Path.Combine(BuildPagePath(post.ID), "index.md");
            }
            else if (post.post_type == "draft")
            {
                filename = "_drafts/" + post.post_slug + ".md";
            }
            else
            {
                filename = "_posts/" + post.post_name;
            }

            string fileDir = Path.GetDirectoryName(filename);
            if (m_verbose > 0) Logger.Log("==== Filename={0}, fileDir={1}", filename, fileDir);
            if (!Directory.Exists(fileDir))
                Directory.CreateDirectory(fileDir);

            using (StreamWriter outt = File.CreateText(filename))
            {
                var serializer = new YamlDotNet.Serialization.Serializer();
                serializer.Serialize(outt, data);
                outt.WriteLine("---");
                outt.Write(post.post_content);
            }
        }
    }

    private void GetPageNameList(MySqlConnection dbcon)
    {
        // Find the Pages
        using (MySqlCommand cmd = new MySqlCommand(
                   String.Format("SELECT "
                                    + "ID,"
                                    + "post_title,"
                                    + "post_name,"
                                    + "post_parent"
                                    + " FROM {0}posts WHERE post_type = 'page'",
                                            m_tablePrefix), dbcon))
        {
            try
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read())
                    {
                        PageName pn = new PageName();
                        ulong pageID = dbReader.GetUInt64("ID");
                        pn.ID = pageID;
                        pn.post_title = dbReader.GetString("post_title");
                        pn.post_name = Sluggify(dbReader.GetString("post_name"));
                        pn.post_parent = dbReader.GetUInt64("post_parent");
                        m_pageNameList[pageID] = pn;

                        if (m_verbose > 0)
                        {
                            Logger.Log("Pages: ID={0}, title={1}, slug={2}, parent={3}",
                                        pageID, pn.post_title, pn.post_name, pn.post_parent);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error reading page names from DB: {0}", e);
            }
        }
    }

    private void GetPosts(MySqlConnection dbcon)
    {
        using (MySqlCommand cmd = new MySqlCommand(
                               String.Format("SELECT "
                                 + "{0}posts.ID,"
                                 + "{0}posts.guid,"
                                 + "{0}posts.post_type,"
                                 + "{0}posts.post_status,"
                                 + "{0}posts.post_title,"
                                 + "{0}posts.post_name,"
                                 + "{0}posts.post_date,"
                                 + "{0}posts.post_date_gmt,"
                                 + "{0}posts.post_content,"
                                 + "{0}posts.post_excerpt,"
                                 + "{0}posts.comment_count,"
                                 + "{0}users.display_name,"
                                 + "{0}users.user_login,"
                                 + "{0}users.user_email,"
                                 + "{0}users.user_url"
                                 + " FROM {0}posts LEFT JOIN {0}users ON {0}posts.post_author = {0}users.ID"
                                 + " WHERE {0}posts.post_status = 'publish'", m_tablePrefix)
                                           , dbcon))
        {
            try
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read())
                    {
                        PostInfo post = new PostInfo();

                        ulong postID = dbReader.GetUInt64("ID");
                        post.ID = postID;
                        post.post_title = dbReader.GetString("post_title");
                        if (m_cleanEntities) post.post_title = CleanEntities(post.post_title);

                        post.guid = dbReader.GetString("guid");
                        post.post_type = dbReader.GetString("post_type");
                        post.post_status = dbReader.GetString("post_status");
                        post.comment_count = dbReader.GetInt32("comment_count");
                        post.display_name = dbReader.GetString("display_name");

                        if (m_verbose > 0)
                        {
                            Logger.Log("Post: ID={0}, title={1}", post.ID, post.post_title);
                        }

                        post.post_slug = dbReader.GetString("post_name");
                        if (String.IsNullOrEmpty(post.post_slug)) post.post_slug = Sluggify(post.post_title);

                        post.post_date = parseTheDate(dbReader, "post_date");
                        post.post_date_gmt = parseTheDate(dbReader, "post_date_gmt");

                        post.post_name = String.Format("{0:d2}-{1:d2}-{2:d2}-{3}.md",
                                post.post_date.Year, post.post_date.Month, post.post_date.Day, post.post_slug);

                        post.post_content = dbReader.GetString("post_content");
                        if (m_cleanEntities) post.post_content = CleanEntities(post.post_content);

                        post.post_excerpt = dbReader.GetString("post_excerpt");

                        int moreIndex = post.post_content.IndexOf("<!--- more --->");
                        string moreAnchor = String.Empty;

                        if (String.IsNullOrEmpty(post.post_excerpt) && moreIndex > 0)
                        {
                            post.post_excerpt = post.post_content.Substring(0, moreIndex);
                        }
                        if (moreIndex > 0)
                        {
                            string content = post.post_content.Replace("<!--- more --->", "<a id=\"more\"></a>");
                            content = content.Replace("<!--- more --->",
                                String.Format("<a id=\"more\"></a><a id=\"more-{0}\"></a>", ((ulong)dbReader["ID"]) ));
                            post.post_content = content;
                        }

                        if (m_verbose > 0)
                            Logger.Log("title='{0}', slug='{1}', date={2}, name='{3}'",
                                       post.post_title, post.post_slug, post.post_date, post.post_name);

                        m_posts.Add(postID, post);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error reading posts DB: {0}", e);
            }
        }
    }

    private void GetTagsAndCategoriesForPost(MySqlConnection dbcon, ulong postID,
                        out List<string> categories, out List<string> tags)
    {
        List<string> tcategories = new List<string>();
        List<string> ttags = new List<string>();

        using (MySqlCommand cmd = new MySqlCommand(
                                    String.Format("SELECT "
                                        + "{0}terms.name,"
                                        + "{0}term_taxonomy.taxonomy"
                                        + " FROM "
                                        + "{0}terms,"
                                        + "{0}term_relationships,"
                                        + "{0}term_taxonomy "
                                        + " WHERE "
                                        + "{0}term_relationships.object_id = '{1}' AND "
                                        + "{0}term_relationships.term_taxonomy_id = {0}term_taxonomy.term_taxonomy_id AND "
                                        + "{0}terms.term_id = {0}term_taxonomy.term_id",
                                        m_tablePrefix, postID) , dbcon) )
        {
            try
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read())
                    {
                        if (((string)dbReader["taxonomy"]) == "category")
                        {
                            string cat = (string)dbReader["name"];
                            if (m_cleanEntities)
                                tcategories.Add(CleanEntities(cat));
                            else
                                tcategories.Add(cat);
                            if (m_verbose > 0) Logger.Log("Category = {0}", cat);
                        }
                        if (((string)dbReader["taxonomy"]) == "post_tag")
                        {
                            string tag = (string)dbReader["name"];
                            if (m_cleanEntities)
                                ttags.Add(CleanEntities(tag));
                            else
                                ttags.Add(tag);
                            if (m_verbose > 0) Logger.Log("Tag = {0}", tag);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error reading tags and categories from DB: {0}", e);
            }
        }
        categories = tcategories;
        tags = ttags;
    }

    private void GetCommentsForPost(MySqlConnection dbcon, ulong postID, out List<CommentInfo> comments)
    {
        // HashSet<CommentInfo> tcomments = new HashSet<CommentInfo>();
        SortedDictionary<ulong, CommentInfo> tcomments = new SortedDictionary<ulong, CommentInfo>();
        
        using (MySqlCommand cmd = new MySqlCommand(
                                    String.Format("SELECT "
                                        + "{0}comments.comment_ID,"
                                        + "{0}comments.comment_author,"
                                        + "{0}comments.comment_author_email,"
                                        + "{0}comments.comment_author_url,"
                                        + "{0}comments.comment_date,"
                                        + "{0}comments.comment_date_gmt,"
                                        + "{0}comments.comment_content"
                                        + " FROM "
                                        + "{0}comments"
                                        + " WHERE "
                                        + "{0}comments.comment_post_ID = '{1}' AND "
                                        + "{0}comments.comment_approved <> 'spam'",
                                        m_tablePrefix, postID) , dbcon) )
        {
            try
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read())
                    {
                        CommentInfo comm = new CommentInfo();
                        comm.comment_ID = dbReader.GetUInt64("comment_ID");
                        comm.author = dbReader.GetString("comment_author");
                        comm.author_email = dbReader.GetString("comment_author_email");
                        comm.author_url = dbReader.GetString("comment_author_url");
                        comm.date = dbReader.GetDateTime("comment_date");
                        // comm.date_gmt = dbReader.GetDateTime("comment_date_gmt");
                        comm.content = dbReader.GetString("comment_content");

                        if (m_cleanEntities)
                        {
                            CleanEntities(comm.content);
                            CleanEntities(comm.author);
                        }

                        tcomments.Add(comm.comment_ID, comm);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error reading comments from DB: {0}", e);
            }
        }

        List<CommentInfo> temp = new List<CommentInfo>();
        foreach (KeyValuePair<ulong, CommentInfo> kvp in tcomments)
        {
            temp.Add(kvp.Value);
        }
        comments = temp;
    }

    private string BuildPagePath(ulong postID)
    {
        string ret = String.Empty;

        PageName pn;
        if (m_pageNameList.TryGetValue(postID, out pn))
        {
            ret = Path.Combine(BuildPagePath(pn.post_parent), pn.post_name);
        }

        return ret;
    }

    private DateTime parseTheDate(MySqlDataReader reader, string column)
    {
        DateTime ret = DateTime.Now;
        try
        {
            ret = reader.GetDateTime("post_date");
        }
        catch
        {
            ret = DateTime.Now;
        }
        return ret;
    }

    private string CleanEntities(string ent)
    {
        return ent;
    }

    // Turn a title into a URLable slug
    //  def self.sluggify( title )
    //    title = title.to_ascii.downcase.gsub(/[^0-9A-Za-z]+/, " ").strip.gsub(" ", "-")
    //  end
    private string Sluggify(string title)
    {
        string ret = title.ToLower();
        ret = ret.Replace("[^0-9A-Za-z]+", " ");
        ret = ret.Trim();
        ret = ret.Replace(" ", "-");
        return ret;
    }

}
}
