require "jekyll-import";

JekyllImport::Importers::WordPress.run({ "dbname"=>"wp_misterblue", "user"=>"root", "password"=>"xxxx", "host"=>"lo
calhost", "table_prefix"=>"wp_", "clean_entities"=>true, "comments"=>true, "categories"=>true, "tags"=>true, "more_excer
pt"=>true, "more_anchor"=>true, "status"=>["publish"] })
