#!/bin/bash


sleep 10

# Change to the liquibase directory
cd /var/www/html/liquibase

# Run Liquibase for website database
liquibase \
    --changelog-file=db.changelog-master-website.yml \
    --url=jdbc:mysql://website:3306/website \
    --username=vowuser \
    --password=password \
    --driver=com.mysql.cj.jdbc.Driver \
    update

# Run Liquibase for API database
liquibase \
    --changelog-file=db.changelog-master-api.yml \
    --url=jdbc:mysql://api:3306/api \
    --username=vowuser \
    --password=password \
    --driver=com.mysql.cj.jdbc.Driver \
    update

# Change back to the original directory
cd /var/www/html

# Start Apache in foreground
apache2-foreground
