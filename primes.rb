#!/usr/bin/env ruby
# frozen_string_literal: true

$VERBOSE = true

require 'prime'

if ARGV.length != 1
  raise ARGV.empty? ? 'too few arguments' : 'too many arguments'
end

upper_bound = Integer(ARGV[0])
puts Prime.take_while { |prime| prime <= upper_bound }.join(' ')
