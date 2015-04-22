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

        Dictionary<ulong, Dictionary<string, string>> pageNameList = new Dictionary<ulong,Dictionary<string,string>>();
        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
        {
            dbcon.Open();

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
                            ulong pageID = (ulong)dbReader["ID"];
                            string pageTitle = (string)dbReader["post_title"];
                            string pageSlug = Sluggify((string)dbReader["post_name"]);
                            ulong pageParent = (ulong)dbReader["post_parent"];
                            Dictionary<string, string> pieces = new Dictionary<string,string>();
                            pieces["pageTitle"] = pageTitle;
                            pieces["pageSlug"] = pageSlug;
                            pieces["pageParent"] = pageParent.ToString();
                            pageNameList[pageID] = pieces;

                            if (m_verbose > 0)
                            {
                                Logger.Log("Pages: ID={0}, title={1}, slug={2}, parent={3}",
                                                        pageID, pageTitle, pageSlug, pageParent);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Error reading DB: {0}", e);
                }
            }


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
                            if (m_verbose > 0)
                            {
                                ulong postID = (ulong)dbReader["ID"];
                                string postTitle = (string)dbReader["post_title"];
                                Logger.Log("Post: ID={0}, title={1}", postID, postTitle);
                            }
                            string pTitle = (string)dbReader["post_title"];
                            if (m_cleanEntities) pTitle = CleanEntities(pTitle);

                            string pSlug = (string)dbReader["post_name"];
                            if (String.IsNullOrEmpty(pSlug)) pSlug = Sluggify(pTitle);

                            DateTime pDate = DateTime.Now;
                            try
                            {
                                pDate = (DateTime)dbReader["post_date"];
                            }
                            catch
                            {
                                pDate = DateTime.Now;
                            }

                            string pName = String.Format("{0:d2}-{1:d2}-{2:d2}-{3}.md", pDate.Year, pDate.Month, pDate.Day, pSlug);

                            string pContent = (string)dbReader["post_content"];
                            if (m_cleanEntities) pContent = CleanEntities(pContent);

                            string pExceprt = (string)dbReader["post_excerpt"];

                            int moreIndex = pContent.IndexOf("<!--- more --->");
                            string moreAnchor = String.Empty;
                            string pExcerpt = (string)dbReader["post_excerpt"];

                            if (String.IsNullOrEmpty(pExcerpt) && moreIndex > 0)
                            {
                                pExceprt = pContent.Substring(0, moreIndex);
                            }
                            if (moreIndex > 0)
                            {
                                pContent.Replace("<!--- more --->", "<a id=\"more\"></a>");
                                pContent.Replace("<!--- more --->",
                                    String.Format("<a id=\"more\"></a><a id=\"more-{0}\"></a>", ((ulong)dbReader["ID"]) ));
                            }

                            if (m_verbose > 0)
                                Logger.Log("title='{0}', slug='{1}', date={2}, name='{3}'", pTitle, pSlug, pDate, pName);


                            HashSet<string> categories = new HashSet<string>();
                            HashSet<string> tags = new HashSet<string>();

                            using (MySqlCommand cmd2 = new MySqlCommand(
                                                        String.Format("SELECT "
                                                            + "{0}terms.name,"
                                                            + "{0}term_taxonomy.taxonomy"
                                                            + " FROM"
                                                            + "{0}terms,"
                                                            + "{0}term_relationships,"
                                                            + "{0}term_taxonomy "
                                                            + " WHERE"
                                                            + "{0}term_relationships.object_id = '{1}' AND "
                                                            + "{0}term_relationships.term_taxonomy_id = {0}term_taxonomy.term_taxonomy_id AND "
                                                            + "{0}terms.term_id = {0}term_taxonomy.term_id", m_tablePrefix, ((ulong)dbReader["ID"])
                                                            , dbcon) ))
                            {
                                try
                                {
                                    using (MySqlDataReader dbReader2 = cmd2.ExecuteReader())
                                    {
                                        while (dbReader2.Read())
                                        {
                                            if (m_verbose > 0)
                                            {
                                                if (((string)dbReader2["term"]) == "category")
                                                {
                                                    string cat = (string)dbReader2["name"];
                                                    if (m_cleanEntities)
                                                        categories.Add(CleanEntities(cat));
                                                    else
                                                        categories.Add(cat);
                                                    if (m_verbose > 0) Logger.Log("Category = {0}", cat);
                                                }
                                                if (((string)dbReader2["term"]) == "post_tag")
                                                {
                                                    string tag = (string)dbReader2["name"];
                                                    if (m_cleanEntities)
                                                        tags.Add(CleanEntities(tag));
                                                    else
                                                        tags.Add(tag);
                                                    if (m_verbose > 0) Logger.Log("Tag = {0}", tag);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.Log("Error reading DB: {0}", e);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Error reading DB: {0}", e);
                }
            }
        }


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
